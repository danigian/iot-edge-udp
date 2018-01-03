As a matter of fact, you can read [the official documentation](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-protocols) that Azure IoT Hub allows devices to use MQTT, AMQP and HTTPS (each of them based on TCP) for device-side communications.

Unfortunately, there is no native support for UDP protocols in Azure IoT Hub and that’s a pity because UDP is fast, lightweight and perfect for low power radio technologies (an example of LPWAN could be [NarrowBand IoT](https://en.wikipedia.org/wiki/NarrowBand_IOT))

# Azure IoT Edge UDP Module #

### Build Azure IoT Edge solutions with UDP connectivity ###

Using this module, developers can build Azure IoT Edge solutions with UDP connectivity. This [Azure IoT Edge](https://github.com/Azure/iot-edge "Azure IoT Edge") module is capable to create a UDP Endpoint, on a defined port, and to route the data payload to the output of the module.

In order to do some tests, there is a prebuilt UDP module container image at danigian/udpmodule:1.0.0 which will simply route the datagram payload to the "output1" of the IoT Edge module. *(Be careful: you should not use this in a production environment)*

# Build and deploy manually #

## How to build ##

First of all, clone this GitHub repository to your computer and eventually review it in order to well understand how this module works.

Navigate to the local folder containing the code

The standard Dockerfiles for a IoT Edge Module, both for Linux and Windows, are located under the "Docker" folder.

Only two commands are needed to build the solution and create the Docker image (you can change linux-x64 with windows-nano). 

    dotnet publish ".\udpmodule.csproj"
	docker build -f ".\Docker\linux-x64\Dockerfile" --build-arg EXE_DIR="./bin/Debug/netcoreapp2.0/publish" -t "udpmodule:latest" .

Properly tag the just created Docker image and push it to your favorite Docker Registry

## How to deploy ##

Please follow these [instructions](https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-csharp-module#run-the-solution) to deploy the IoT Edge module.

In the **Container Create Option section** enter the following to properly configure and open the default 1208/UDP Port on which the server will be receiving datagrams.

	{
	  "ExposedPorts": {
	    "1208/udp": {}
	  },
	  "HostConfig": {
	    "PortBindings": {
	      "1208/udp": [
	        {
	          "HostPort": "1208"
	        }
	      ]
	    }
	  }
	}

If you want to expose a different port, you need to configure the **EdgeUdpPort** environment variable. Here it is an example of the proper **Container Create Option section** 

	{
	  "Env": [
		"EdgeUdpPort=1209"
	  ],
	  "ExposedPorts": {
	    "1209/udp": {}
	  },
	  "HostConfig": {
	    "PortBindings": {
	      "1209/udp": [
	        {
	          "HostPort": "1209"
	        }
	      ]
	    }
	  }
	}

The datagram payload will be written to the "output1" output of the IoT Edge module.
If you just want to route these data to IoT Hub, in the **Specify Routes section** you should have something like the following JSON

	{
		"routes":{
			"UDPToIoTHub":"FROM /messages/modules/udpModule/outputs/output1 INTO $upstream"
		}
	} 

You can now send your data to the opened UDP port and test if everything worked properly.

> Because of a known behavior in WinNAT, if using Windows Nano Server, you will not be able to send UDP packets to localhost:1208 for testing. You will need to use the "docker inspect" command in order to find the container IP address and then test using that IP address.

# Build and deploy using Azure IoT Edge Dev Tool #

[Azure IoT Edge Dev Tool](https://github.com/jonbgallant/azure-iot-edge-dev-tool) greatly simplifies your Azure IoT Edge development process. It includes all the tools you need to work with your IoT Edge Projects.

Integrating this sample module into IoT Edge Dev Tool is really simple and fast.

1. Setup the Dev Machine and create an Azure IoT Edge Project
2. Under the *module* folder, create a folder named *udpmodule*
3. Copy every file from this repository in the *udpmodule* folder
4. Add Module to Config
	1. Open /config/modules.json
	2. Copy and paste the following JSON under moduleContent.$edgeAgent.properties.desired.modules (*eventually modifying port from 1209 to the desired one*)
	
			"udpmodule": {
				"version": "1.0",
				"type": "docker",
				"status": "running",
				"restartPolicy": "always",
				"settings": {
				  "image": "${CONTAINER_REGISTRY_SERVER}/udpmodule:linux-x64-${CONTAINER_TAG}",
				  "createOptions": "{\"Env\":[\"EdgeUdpPort=1209\"],\"ExposedPorts\":{\"1209/udp\":{}},\"HostConfig\":{\"PortBindings\":{\"1209/udp\":[{\"HostPort\":\"1209\"}]}}}"
				}
			}
5. Add Route to Config
	1. Open /config/modules.json
	2. Under $edgeHub.properties.desired.routes add your key/value property in order to route datagrams to the desired module (*example below which routes datagrams from output1 of udpmodule to input1 of analyzeandfilter*)

	          "udptoanalyze": "FROM /messages/modules/udpmodule/outputs/output1 INTO BrokeredEndpoint(\"/modules/analyzeandfilter/inputs/input1\")"
6. Build and Deploy Modules with the following command:

		iotedgedev modules --build --deploy

That's it!
