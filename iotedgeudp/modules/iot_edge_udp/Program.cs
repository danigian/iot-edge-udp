namespace iot_edge_udp
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

    class Program
    {
        static ModuleClient ioTHubModuleClient;
        static void Main(string[] args)
        {
            Init().Wait();

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
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
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
                Console.WriteLine($"Using port {udpPort}");
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
