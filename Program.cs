namespace udpmodule
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using System.Net.Sockets;
    using System.Net;

    class Program
    {
        static DeviceClient ioTHubModuleClient;
        static void Main(string[] args)
        {
            // The Edge runtime gives us the connection string we need -- it is injected as an environment variable
            string connectionString = Environment.GetEnvironmentVariable("EdgeHubConnectionString");

            // Cert verification is not yet fully functional when using Windows OS for the container
            bool bypassCertVerification = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (!bypassCertVerification) InstallCert();
            Init(connectionString, bypassCertVerification).Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Add certificate in local cert store for use by client for secure connection to IoT Edge runtime
        /// </summary>
        static void InstallCert()
        {
            string certPath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");
            if (string.IsNullOrWhiteSpace(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing path to certificate file.");
            }
            else if (!File.Exists(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing certificate file.");
            }
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate2.CreateFromCertFile(certPath)));
            Console.WriteLine("Added Cert: " + certPath);
            store.Close();
        }


        /// <summary>
        /// Initializes the DeviceClient and sets up the UDP Server to receive
        /// messages to be routed on the output of the module
        /// </summary>
        static async Task Init(string connectionString, bool bypassCertVerification = false)
        {
            Console.WriteLine("Connection String {0}", connectionString);

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            // During dev you might want to bypass the cert verification. It is highly recommended to verify certs systematically in production
            if (bypassCertVerification)
            {
                mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ioTHubModuleClient = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            //Set a new Thread for "UDP Server"
            Thread thdUDPServer = new Thread(new ThreadStart(UDPServer));
            thdUDPServer.Start();
        }

        /// <summary>
        /// This method starts a "UDP Server". 
        /// The port of this UDP Server is specified in the environment variable EdgeUdpPort.
        /// If not specified the UdpClient will start on 1208/udp port.
        /// The UDP Payload will be written to the "output1" of the module
        /// </summary>
        static void UDPServer()
        {
            int udpPort;
            //Try to get the UDP port on which the server will be started. If not available, the module will use the 1208 as default port
            try
            {
                udpPort = int.Parse(Environment.GetEnvironmentVariable("EdgeUdpPort"));
                Console.WriteLine("Using port 1209");
            }
            catch(ArgumentNullException)
            {
                Console.WriteLine("Port not specified. Will start the UDP Server on 1208/udp port");
                udpPort = 1208;
            }
            catch(FormatException)
            {
                Console.WriteLine("Port is not in the correct format. Will start the UDP Server on 1208/udp port");
                udpPort = 1208;
            }

            UdpClient _udpClient = new UdpClient(udpPort);
            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                try
                {
                    //Be careful, as the Receive method will block until a datagram arrives from a remote host
                    Byte[] receiveBytes = _udpClient.Receive(ref RemoteIpEndPoint);

                    /*
                     * You may want to add some piece of code here, to format your data as a JSON for better interoperability with other modules
                     */

                    //Writing the UDP payload to "output1" of the module
                    ioTHubModuleClient.SendEventAsync("output1", new Message(receiveBytes));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}
