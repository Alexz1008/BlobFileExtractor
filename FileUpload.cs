using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using System;
using System.IO;
using System.Threading.Tasks;
using static System.Text.Encoding;
using Ionic.Zip;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Net;
using System.Collections.Generic;

namespace FileUploadFunction
{
    public static class FileUpload
    {
        [FunctionName("FileUpload")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req, ILogger log)
        {
            // Configuration
            string Connection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = "testcontainer";
            var file = req.Form.Files["File"];
            Stream myBlob = file.OpenReadStream();
            var blobClient = new BlobContainerClient(Connection, containerName);
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) + "\\";
            var password = "Y@qu1naBayLig7t";
            var tempFilePath = tempPath + file.FileName;
            var zipDestPath = tempPath + "unzip";
            var reqUrl = "https://prod-19.centralus.logic.azure.com:443/workflows/63adf88c7de647b6ba4946ca8d88d1db/triggers/manual/paths/invoke?api-version=2016-10-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=L_QH0v41gtej9_uXtPIjvecvchy9kT30LKEsZxkqscQ";

            /*
            Directory.CreateDirectory(tempPath);
            Directory.CreateDirectory(zipDestPath);

            // Puts zip file in a folder
            using (var fileStream = File.Create(tempFilePath))
            {
                myBlob.CopyTo(fileStream);
            }

            // Unzips the file
            RegisterProvider(CodePagesEncodingProvider.Instance);
            ZipFile zip = ZipFile.Read(tempFilePath);
            zip.Password = password;
            foreach (ZipEntry e in zip)
            {
                e.Extract(zipDestPath, ExtractExistingFileAction.OverwriteSilently);
                log.LogInformation("Extracting file from zip: " + e.FileName);
            }
            zip.Dispose();
            File.Delete(tempFilePath);

            // Processes the extracted files
            foreach (var path in Directory.GetFiles(zipDestPath))
            {
                // Upload any file except gpg
                if (!path.EndsWith(".gpg"))
                {
                    var fileinfo = new FileInfo(path);
                    var blob = blobClient.GetBlobClient(fileinfo.Name);
                    using (var fileStream = File.Create(path))
                    {
                        await blob.UploadAsync(fileStream);
                    }
                    log.LogInformation("Uploading file to blob: " + fileinfo.Name);
                }

                // Parse json of gpg
                else
                {

                }
            }

            // Sends a json of the file to the next logic app
            */
            List<ORBSForm> list = new List<ORBSForm>();
            list.Add(new ORBSForm("TesterName"));
            log.LogInformation("Test 1 " + list);
            String json = JsonSerializer.Serialize(list);
            log.LogInformation("Serialized json: " + json);
            log.LogInformation("Serialized json 2: " + JsonSerializer.Serialize(new ORBSForm("TestName")));

            // create a request
            HttpWebRequest request = (HttpWebRequest)
            WebRequest.Create(reqUrl); request.KeepAlive = false;
            request.ProtocolVersion = HttpVersion.Version10;
            request.Method = "POST";

            // turn our request string into a byte stream
            byte[] postBytes = Encoding.UTF8.GetBytes(json);

            // this is important - make sure you specify type this way
            request.ContentType = "application/json; charset=UTF-8";
            request.Accept = "application/json";
            request.ContentLength = postBytes.Length;
            Stream requestStream = request.GetRequestStream();

            // now send it
            requestStream.Write(postBytes, 0, postBytes.Length);
            requestStream.Close();

            // grab the response and print it out to the console along with the status code
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            string result;
            using (StreamReader rdr = new StreamReader(response.GetResponseStream()))
            {
                result = rdr.ReadToEnd();
            }

            return new OkObjectResult("Completed transaction");
        }

        private static String decryptGpg(String path)
        {
            return "";
        }
    }

    public class ORBSForm
    {
        public String name { get; set; }

        public ORBSForm(String name)
        {
            this.name = name;
        }
    }
}