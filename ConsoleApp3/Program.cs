using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Ami;

namespace ConsoleApp3
{
    class Program
    {
        public static async Task Main(String[] args)
        {
            // To make testing possible, an AmiClient accepts any Stream object
            // that is readable and writable. This means that the user is
            // responsible for maintaining a TCP connection to the AMI server.

            // It's actually pretty easy...

            using (var socket = new TcpClient(hostname: "127.0.0.1", port: 5038))
            using (var client = new AmiClient(socket.GetStream()))
            {
                // At this point, we've completed the AMI protocol handshake and
                // a background I/O Task is consuming data from the socket.

                // Activity on the wire can be observed and logged using the
                // DataSent and DataReceived events...

                client.DataSent += (s, e) => Console.Error.Write(e.Data);
                client.DataReceived += (s, e) => Console.Error.Write(e.Data);

                // First, let's authenticate using the Login() helper function...

                if (!await client.Login(username: "admin", secret: "amp111", md5: true))
                {
                    Console.WriteLine("Login failed");
                    return;
                }

                // In case the Asterisk server hasn't finished booting, let's wait
                // until it's ready...

                await client.Where(message => message["Event"] == "FullyBooted").FirstAsync();

                // Now let's issue a PJSIPShowEndpoints command...

                var response = await client.Publish(new AmiMessage
         {
            { "Action", "PJSIPShowEndpoints" },
         });

                // Because we didn't specify an ActionID, one was implicitly
                // created for us by the AmiMessage object. That's how we track
                // requests and responses, allowing this client to be used
                // by multiple threads or tasks.

                if (response["Response"] == "Success")
                {
                    // After the PJSIPShowEndpoints command successfully executes,
                    // Asterisk will begin emitting EndpointList events.

                    // Each EndpointList event represents a single PJSIP endpoint,
                    // and has the same ActionID as the PJSIPShowEndpoints command
                    // that caused it.

                    // Once events have been emitted for all PJSIP endpoints,
                    // an EndpointListComplete event will be emitted, again with
                    // the same ActionID as the PJSIPShowEndpoints command
                    // that caused it.

                    // Using System.Reactive.Linq, all of that can be modeled with
                    // a simple Rx IObservable consumer...

                    await client
                       .Where(message => message["ActionID"] == response["ActionID"])
                       .TakeWhile(message => message["Event"] != "EndpointListComplete")
                       .Do(message => Console.Out.WriteLine($"~~~ \"{message["ObjectName"]}\" ({message["DeviceState"]}) ~~~"));
                }

                // We're done, so let's be a good client and use the Logoff()
                // helper function...

                if (!await client.Logoff())
                {
                    Console.WriteLine("Logoff failed");
                    return;
                }
            }
        }
    }
}
