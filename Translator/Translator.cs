using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;
using Translator.Models;

namespace Translator
{
    public static class Translator
    {
        [FunctionName("translate")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string incommingRequestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Input data = JsonConvert.DeserializeObject<Input>(incommingRequestBody);

            if (data.OutputLanguage == string.Empty || data.OutputLanguage == null)
            {
                return new BadRequestObjectResult("Missing Output Language.");
            }

            if (data.Content == string.Empty || data.Content == null)
            {
                return new BadRequestObjectResult("Missing Content.");
            }

            Object[] body = new Object[] { new { Text = data.Content } };
            var requestBody = JsonConvert.SerializeObject(body);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Set the method to POST
                request.Method = HttpMethod.Post;

                // Construct the full URI
                request.RequestUri = new Uri($"{configuration.GetValue<string>("TranslatorApiUrl")}/translate?api-version=3.0&to={data.OutputLanguage}");

                // Add the serialized JSON object to your request
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // Add the authorization header
                request.Headers.Add("Ocp-Apim-Subscription-Key", configuration.GetValue<string>("TranslatorApiKey"));

                // Send request, get response
                var response = client.SendAsync(request).Result;
                var jsonResponse = response.Content.ReadAsStringAsync().Result;

                // Convert to a easier output
                RootObject[] objectResponse = JsonConvert.DeserializeObject<RootObject[]>(jsonResponse);
                Output output = new Output()
                {
                    Content = objectResponse[0].translations[0].text,
                    InputLanguage = objectResponse[0].detectedLanguage.language,
                    Score = objectResponse[0].detectedLanguage.score
                };

                return jsonResponse != null
                ? (ActionResult)new OkObjectResult(output)
                : new BadRequestObjectResult("Issue while translating");
            }
        }
    }
}