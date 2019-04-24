
using Newtonsoft.Json;
using ZeroMQ;
using IniParser;
using IniParser.Model;
using System.Diagnostics;

using System;
using System.Collections.Generic;

public class TyphoonAPI
{
    string name;
    String port;
    static int sequence = 0;
    static bool tried_start = false;

    static int LPClient_RequestRetries = 20;

    public TyphoonAPI(string name)
    {
        this.name = name;                

        var parser = new FileIniDataParser();
        IniData data = parser.ReadFile("C:\\ProgramData\\typhoon\\settings.conf");
        port = data[name]["server_rep_port"];

        if (!tried_start)
        {
            // Run Typhoon HIL Server (does nothing if already opened)
            String TyphoonHILCC = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("TYPHOON"), "typhoon_hil.exe");
            Console.WriteLine(TyphoonHILCC);
            Process.Start(TyphoonHILCC, "-hapi");
            tried_start = true;
        }

        Call(
            "ping",
            new Dictionary<string, object> { },
            TimeSpan.FromMilliseconds(1000)
        );

    }

    public ZFrame MakeZRequest(int seq, String functionName, Dictionary<string, object> parameters)
    {
        // Build request Message
        Dictionary<string, object> message = new Dictionary<string, object>
            {
                { "jsonrpc", "2.0" },  // mandatory field, must be 2.0
                { "api", "1.0" }, // mandatory field
                { "id", seq }, // mandatory field, should be increased with every request
                { "method", functionName }, // mandatory field
                { "params", parameters },
            };

        // Build request message
        return new ZFrame(JsonConvert.SerializeObject(message));
    }

    //
    // Lazy Pirate client
    // Use zmq_poll (pollItem.PollIn) to do a safe request-reply
    // To run, start lpserver and then randomly kill/restart it
    //
    // Author: metadings
    //
    public ZSocket LPClient_CreateZSocket(ZContext context, out ZError error)
    {
        // Helper function that returns a new configured socket
        // connected to the Lazy Pirate queue

        var requester = new ZSocket(context, ZSocketType.REQ);
        requester.IdentityString = name;
        requester.Linger = TimeSpan.FromMilliseconds(1);

        if (!requester.Connect("tcp://localhost:"+port, out error))
        {
            return null;
        }
        return requester;
    }

    public object Call(String functionName, Dictionary<string, object> parameters, TimeSpan? timeout = null)
    {

        using (var context = new ZContext())
        {
            ZSocket requester = null;
            try
            { // using (requester)

                ZError error;

                if (null == (requester = LPClient_CreateZSocket(context, out error)))
                {
                    throw new ZException(error);
                }

                int retries_left = LPClient_RequestRetries;
                var poll = ZPollItem.CreateReceiver();

                while (retries_left > 0)
                {
                    // We send a request, then we work to get a reply
                    using (var outgoing = MakeZRequest(++sequence, functionName, parameters))
                    {
                        if (!requester.Send(outgoing, out error))
                        {
                            throw new ZException(error);
                        }
                    }

                    ZMessage incoming;
                    while (true)
                    {
                        // Here we process a server reply and exit our loop
                        // if the reply is valid.

                        // If we didn't a reply, we close the client socket
                        // and resend the request. We try a number of times
                        // before finally abandoning:

                        // Poll socket for a reply, with timeout
                        if (requester.PollIn(poll, out incoming, out error, timeout))
                        {
                            using (incoming)
                            {
                                // We got a reply from the server
                                String incoming_str = incoming[0].ReadString();
                                Console.WriteLine(incoming_str);
                                Dictionary<string, object> resp_message = JsonConvert.DeserializeObject<Dictionary<string, object>>(incoming_str);
                                int incoming_sequence = int.Parse(resp_message["id"].ToString());
                                if (sequence == incoming_sequence)
                                {
                                    Console.WriteLine("I: server replied OK ({0})", incoming_sequence);
                                    retries_left = LPClient_RequestRetries;
                                    if (resp_message.ContainsKey("error")) {
                                        Dictionary<string, object> error_resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(resp_message["error"].ToString());
                                        throw new Exception(error_resp["message"].ToString());
                                    }
                                    if (resp_message.ContainsKey("warnings"))
                                    {
                                        Console.WriteLine(resp_message["warnings"].ToString());
                                        if (resp_message.ContainsKey("result"))
                                        {
                                            if (resp_message["result"] is bool && (bool) resp_message["result"] == false)
                                            {
                                                throw new Exception("Function call returned false");
                                            }
                                        }
                                    }
                                    return resp_message["result"];
                                }
                                else
                                {
                                    Console.WriteLine("E: malformed reply from server", incoming);
                                }
                            }
                        }
                        else
                        {
                            if (error == ZError.EAGAIN)
                            {
                                if (--retries_left == 0)
                                {
                                    throw new Exception("E: server seems to be offline, abandoning");
                                }

                                Console.WriteLine("W: no response from server, retrying… ({0})", retries_left);

                                // Old socket is confused; close it and open a new one
                                requester.Dispose();
                                if (null == (requester = LPClient_CreateZSocket(context, out error)))
                                {
                                    throw new ZException(error);
                                }

                                Console.WriteLine("I: reconnected");

                                // Send request again, on new socket
                                using (var outgoing = MakeZRequest(++sequence, functionName, parameters))
                                {
                                    if (!requester.Send(outgoing, out error))
                                    {
                                        throw new ZException(error);
                                    }
                                }

                                continue;
                            }

                            throw new ZException(error);
                        }
                    }
                }
            }
            finally
            {
                if (requester != null)
                {
                    requester.Dispose();
                    requester = null;
                }
            }
            throw new Exception("Code should not reach this part.");
        }
    }
}
