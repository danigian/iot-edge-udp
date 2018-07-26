As a matter of fact, you can read [the official documentation](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-protocols) that Azure IoT Hub allows devices to use MQTT, AMQP and HTTPS (each of them based on TCP) for device-side communications.

Unfortunately, there is no native support for UDP protocols in Azure IoT Hub and that’s a pity because UDP is fast, lightweight and perfect for low power radio technologies (an example of LPWAN could be [NarrowBand IoT](https://en.wikipedia.org/wiki/NarrowBand_IOT))

# Azure IoT Edge UDP Solution (now works with GA bits!) #

### Build Azure IoT Edge solutions with UDP connectivity ###

Using this module, developers can build Azure IoT Edge solutions with UDP connectivity. This [Azure IoT Edge](https://docs.microsoft.com/en-us/azure/iot-edge/ "Azure IoT Edge") module is capable to create a UDP Endpoint, on a defined port, and to route the data payload to the output of the module.

In order to do some tests, there is a prebuilt UDP module container image at danigian/udpmodule:1.0.0-ga-amd64 which will simply route the datagram payload to the "output1" of the IoT Edge module. *(Be careful: you should not use this in a production environment)*

# Build and deploy with Visual Studio Code #

First of all, clone this GitHub repository to your computer and review it in order to well understand how this module works.

If you want to change the listening port for the module (EdgeUdpPort) change it in the _createOptions_ section of the iot\_edge\_udp module in the deployment.template.json file 

To build the solution, first of all properly change the repository name and tag in the modules\module.json file which should look like this:

    {
	    "$schema-version": "0.0.1",
	    "description": "",
	    "image": {
	        "repository": "danigian/udpmodule",
	        "tag": {
	            "version": "1.0.0-ga",
	            "platforms": {
	                "amd64": "./Dockerfile.amd64", 
	                "amd64.debug": "./Dockerfile.amd64.debug",
	                "arm32v7": "./Dockerfile.arm32v7",
	                "windows-amd64": "./Dockerfile.windows-amd64"
	            }
	        },
	        "buildOptions": []
	    },
	    "language": "csharp"
	}

Navigate to the deployment.template.json file and update your function image URL by selecting the proper platform for your device (i.e.: _"image": "${MODULES.iot\_edge\_udp.arm32v7}"_)

In the VS Code command palette, enter and run the command **"Edge: Build IoT Edge solution"** and select the deployment.template.json file. This operation will build and publish on your repository the docker image for the modules.

To deploy, in the VS Code tab for Azure IoT Hub Device Explorer, right-click the IoT Edge device ID on which you want to publish the solution. Then click **Create deployment for IoT Edge device** and select the config\deployment.json file.

Done! 

You can now check your container status by running the _docker ps_ command.

If it is running, try to send your data to localhost:EdgeUdpPort and test if the message goes to IoT Hub properly (in VS Code right click the device and select **Start monitoring D2C message**)

> Because of a known behavior in WinNAT, if using Windows Nano Server, you will not be able to send UDP packets to localhost:1208 for testing. You will need to use the "docker inspect" command in order to find the container IP address and then test using that IP address.

