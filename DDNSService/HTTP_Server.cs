using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DDNSService
{
	class HTTP_Server
	{
        public static HttpListener listener;
        public static bool AkaFirewallFilter => true;

        static bool runServer = true;
        static int requestCount = 0;

        public static void StartApi(int port)
        {
            Console.WriteLine("Starting http server on port: " + port);
            Console.WriteLine();

            listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{port}/");

            try
            {
                listener.Start();
            }
            catch (HttpListenerException exc)
            {
                Console.WriteLine("ERROR: CANNOT BIND PORT TO \"*\" SO BINDING TO \"localhost\"");
                if (exc.ErrorCode == 5) //if on local pc
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add($"http://localhost:{port}/");
                    listener.Start();
                }
            }

            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            listener.Close();
        }

        public static async Task HandleIncomingConnections()
        {
            while (runServer)
            {
                HttpListenerContext ctx = await listener.GetContextAsync();

                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                try
                {
                    string rawurl = req.RawUrl.Remove(0, 1);

                    if ((req.UserHostName == "ddns.filipton.space" && AkaFirewallFilter) || !AkaFirewallFilter)
                    {
                        if (req.Url.AbsolutePath != "/favicon.ico")
                        {
                            Console.WriteLine($"Request #: {++requestCount} ({req.Headers.Get("X-Real-IP")})");
                            Console.WriteLine("/" + rawurl);
                            Console.WriteLine(req.HttpMethod);
                            Console.WriteLine(req.UserAgent);
                            Console.WriteLine();

                            string[] args = rawurl.Split('/', StringSplitOptions.RemoveEmptyEntries);

                            if(args.Length > 0)
							{
                                switch (args[0])
                                {
                                    case "create":
                                        if (args.Length == 3)
										{
                                            await SendResponse(resp, Program.CreateRecord(args[1], args[2]));
                                        }
										else
										{
                                            await SendResponse(resp, "ARGUMENTS COUNT NOT PASSED!");
                                        }
                                        break;
                                    case "change":
                                        if (args.Length == 3)
                                        {
                                            await SendResponse(resp, Program.ChangeIpOfRecord(args[1], args[2]));
                                        }
                                        else
                                        {
                                            await SendResponse(resp, "ARGUMENTS COUNT NOT PASSED!");
                                        }
                                        break;
                                    case "delete":
                                        if (args.Length == 2)
                                        {
                                            await SendResponse(resp, Program.DeleteRecord(args[1]));
                                        }
                                        else
                                        {
                                            await SendResponse(resp, "ARGUMENTS COUNT NOT PASSED!");
                                        }
                                        break;
                                    case "list":
                                        await SendResponse(resp, JsonConvert.SerializeObject(Program.dnsRecords, Formatting.Indented));
                                        break;
                                    case "update":
                                        if (args.Length == 2)
                                        {
                                            await SendResponse(resp, Program.ChangeIpOfRecord(args[1], req.Headers.Get("X-Real-IP")));
                                        }
                                        else
                                        {
                                            await SendResponse(resp, "ARGUMENTS COUNT NOT PASSED!");
                                        }
                                        break;
                                    default:
                                        await SendResponse(resp, "END POINT NOT FOUNDED!");
                                        break;
                                }
                            }
							else
							{
                                await SendResponse(resp, $"Its simple opensource ddns service working with desec. <br> Feel free to use on your server! <br> If you found a bug you can report it on github page or fix it and start pull request!");
                            }
                        }
						else
						{
                            await SendResponse(resp, "NOT FOUND");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERROR! " + e.Message);
                    await SendResponse(resp, "ERROR!");
                }
            }

        }

        public static async Task SendResponse(HttpListenerResponse resp, string response)
        {
            byte[] data = Encoding.UTF8.GetBytes(response);
            resp.ContentType = "text/html";
            resp.ContentEncoding = Encoding.UTF8;
            resp.ContentLength64 = data.LongLength;

            await resp.OutputStream.WriteAsync(data, 0, data.Length);
            resp.Close();
        }
    }
}
