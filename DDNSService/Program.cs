using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace DDNSService
{
	public struct DnsRecord
	{
		public string subName;
		public string ip;

		public DnsRecord(string sn, string ip)
		{
			subName = sn;
			this.ip = ip;
		}
	}

	class Program
	{
		public static bool ReAuthWhenUnauthorized => false;

		public static string id = "";
		public static string token = "";

		public static string Username = "";
		public static string Password = "";

		public static string domainName = "";

		public static List<DnsRecord> dnsRecords = new List<DnsRecord>();

		static void Main(string[] args)
		{
			if (File.Exists("account.txt"))
			{
				string[] acc = File.ReadAllText("account.txt").Split(':');

				if(acc.Length == 2)
				{
					Username = acc[0];
					Password = acc[1];
				}
				else
				{
					Console.WriteLine("FILE account.txt STRUCTURE BROKEN! ABORTING! (file structure: email:password)");
					return;
				}
			}
			else
			{
				Console.WriteLine("FILE account.txt NOT FOUNDED! ABORTING! (file structure: email:password)");
				return;
			}

			if (File.Exists("token.txt"))
			{
				string[] tmp = File.ReadAllText("token.txt").Split(':');

				id = tmp[0];
				token = tmp[1];

				//checking token
				CheckAuthToken();
			}
			else
			{
				GenerateAuthToken(true);
			}

			if (File.Exists("domain.txt"))
			{
				domainName = File.ReadAllText("domain.txt");
			}
			else
			{
				GetAllDomainsFromAccount();
			}

			GetAllRecordsFromDomain();


			new Thread(() =>
			{
				HTTP_Server.StartApi(5431);
			}).Start();
		}

		public static string GenerateAuthToken(bool skipReauth = false)
		{
			if (ReAuthWhenUnauthorized || skipReauth)
			{
				using (var httpClient = new HttpClient())
				{
					using (var request = new HttpRequestMessage(new HttpMethod("POST"), "https://desec.io/api/v1/auth/login/"))
					{
						request.Content = new StringContent("{\"email\":\"" + Username + "\",\"password\":\"" + Password + "\"}");
						request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

						var response = httpClient.SendAsync(request);
						dynamic json = JsonConvert.DeserializeObject(response.Result.Content.ReadAsStringAsync().Result);
						token = json["token"];
						id = json["id"];
						Console.WriteLine(id + ":" + token);
						File.WriteAllText("token.txt", id + ":" + token);
					}
				}
			}

			return string.Empty;
		}

		public static void CheckAuthToken()
		{
			using (var httpClient = new HttpClient())
			{
				using (var request = new HttpRequestMessage(new HttpMethod("GET"), $"https://desec.io/api/v1/auth/tokens/{id}/"))
				{
					request.Headers.TryAddWithoutValidation("Authorization", $"Token {token}");

					var response = httpClient.SendAsync(request);
					if (response.Result.StatusCode == System.Net.HttpStatusCode.Unauthorized)
					{
						GenerateAuthToken();
					}
				}
			}
		}

		public static void GetAllDomainsFromAccount()
		{
			using (var httpClient = new HttpClient())
			{
				using (var request = new HttpRequestMessage(new HttpMethod("GET"), $"https://desec.io/api/v1/domains/"))
				{
					request.Headers.TryAddWithoutValidation("Authorization", $"Token {token}");

					var response = httpClient.SendAsync(request);
					if (response.Result.StatusCode == System.Net.HttpStatusCode.Unauthorized)
					{
						GenerateAuthToken();
					}
					else
					{
						dynamic json = JsonConvert.DeserializeObject(response.Result.Content.ReadAsStringAsync().Result);
						int domainsCount = json.Count;
						for(int i = 0; i < domainsCount; i++)
						{
							string name = json[i]["name"];

							if (name.Contains("ddns"))
							{
								domainName = name;
								Console.WriteLine("FOUND ONE: " + name);
								File.WriteAllText("domain.txt", name);
							}
						}
					}
				}
			}
		}

		public static void GetAllRecordsFromDomain()
		{
			using (var httpClient = new HttpClient())
			{
				using (var request = new HttpRequestMessage(new HttpMethod("GET"), $"https://desec.io/api/v1/domains/{domainName}/rrsets/?type=A"))
				{
					request.Headers.TryAddWithoutValidation("Authorization", $"Token {token}");

					var response = httpClient.SendAsync(request);

					if (response.Result.StatusCode == System.Net.HttpStatusCode.Unauthorized)
					{
						GenerateAuthToken();
					}
					else
					{
						dynamic json = JsonConvert.DeserializeObject(response.Result.Content.ReadAsStringAsync().Result);
						int recordsCount = json.Count;

						for (int i = 0; i < recordsCount; i++)
						{
							if((int)json[i]["records"].Count > 0)
							{
								string subname = json[i]["subname"];
								string ip = json[i]["records"][0];

								if(subname != string.Empty)
								{
									if (dnsRecords.FindIndex(x => x.subName == subname) < 0)
									{
										dnsRecords.Add(new DnsRecord(subname, ip));
										Console.WriteLine($"{subname}: {ip}");
									}
								}
							}
						}
					}
				}
			}
		}

		public static string ChangeIpOfRecord(string subname, string ipToChange)
		{
			int index = dnsRecords.FindIndex(x => x.subName == subname);
			if (index >= 0)
			{
				using (var httpClient = new HttpClient())
				{
					using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"https://desec.io/api/v1/domains/{domainName}/rrsets/{subname}/A/"))
					{
						request.Headers.TryAddWithoutValidation("Authorization", $"Token {token}");

						request.Content = new StringContent("{\"records\":[\"" + ipToChange + "\"]}");
						request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

						var response = httpClient.SendAsync(request);
						if (response.Result.StatusCode == System.Net.HttpStatusCode.Unauthorized)
						{
							GenerateAuthToken();
							return "UNATHORIZED";
						}
						else
						{
							dynamic json = JsonConvert.DeserializeObject(response.Result.Content.ReadAsStringAsync().Result);
							dnsRecords[index] = new DnsRecord(subname, ipToChange);
							return "CHANGED";
						}
					}
				}
			}

			return "SUBDOMAIN NOT FOUNDED";
		}

		public static string CreateRecord(string subname, string ip)
		{
			if (dnsRecords.FindIndex(x => x.subName == subname) < 0)
			{
				using (var httpClient = new HttpClient())
				{
					using (var request = new HttpRequestMessage(new HttpMethod("POST"), $"https://desec.io/api/v1/domains/{domainName}/rrsets/"))
					{
						request.Headers.TryAddWithoutValidation("Authorization", $"Token {token}");

						request.Content = new StringContent("{\"subname\": \"" + subname + "\", \"type\": \"A\", \"ttl\": 3600, \"records\": [\"" + ip + "\"]}");
						request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

						var response = httpClient.SendAsync(request);

						if (response.Result.StatusCode == System.Net.HttpStatusCode.Unauthorized)
						{
							GenerateAuthToken();
							return "UNATHORIZED";
						}
						else
						{
							dynamic json = JsonConvert.DeserializeObject(response.Result.Content.ReadAsStringAsync().Result);
							dnsRecords.Add(new DnsRecord(subname, ip));
							return "CREATED";
						}
					}
				}
			}

			return "DOMAIN ALREADY EXISTS!";
		}

		public static string DeleteRecord(string subname)
		{
			int index = dnsRecords.FindIndex(x => x.subName == subname);
			if (index >= 0)
			{
				using (var httpClient = new HttpClient())
				{
					using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"https://desec.io/api/v1/domains/{domainName}/rrsets/"))
					{
						request.Headers.TryAddWithoutValidation("Authorization", $"Token {token}");

						request.Content = new StringContent("[ {\"subname\": \"" + subname + "\", \"type\": \"A\", \"records\": []} ]");
						request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

						var response = httpClient.SendAsync(request);

						if (response.Result.StatusCode == System.Net.HttpStatusCode.Unauthorized)
						{
							GenerateAuthToken();
							return "UNATHORIZED";
						}
						else
						{
							dynamic json = JsonConvert.DeserializeObject(response.Result.Content.ReadAsStringAsync().Result);
							dnsRecords.RemoveAt(index);
							return "DELETED";
						}
					}
				}
			}

			return "SUBDOMAIN NOT FOUNDED!";
		}
	}
}
