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
using PgpCore;

namespace FileUploadFunction
{
    public static class FileUpload
    {
        // Configuration
        static string Connection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        static string containerName = "testcontainer";
        static string zipPassword = "Y@qu1naBayLig7t";
        static string ascPassword = "80a7-b6c549791522DDAF9583-2F05-4437-A1F9-##";
        static string reqUrl = "https://prod-19.centralus.logic.azure.com:443/workflows/63adf88c7de647b6ba4946ca8d88d1db/triggers/manual/paths/invoke?api-version=2016-10-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=L_QH0v41gtej9_uXtPIjvecvchy9kT30LKEsZxkqscQ";
        [FunctionName("FileUpload")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req, ILogger log)
        {
            // Setting up temporary environment
            var file = req.Form.Files["File"];
            Stream myBlob = file.OpenReadStream();
            var blobClient = new BlobContainerClient(Connection, containerName);
            var secretsBlobClient = new BlobContainerClient(Connection, "secrets");
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) + "\\";
            var tempFilePath = tempPath + file.FileName;
            var tempSecretPath = tempPath + "key.asc";
            var tempSecretDecryptedPath = tempPath + "decrypted.txt";
            var zipDestPath = tempPath + "unzip";

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
            zip.Password = zipPassword;
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
                    log.LogInformation("Decrypting file: " + path);
                    await secretsBlobClient.GetBlobClient("ORUATPrivateKey.asc").DownloadToAsync(tempSecretPath);

                    FileInfo privateKey = new FileInfo(tempSecretPath);
                    EncryptionKeys encryptionKeys = new EncryptionKeys(privateKey, ascPassword);

                    // Reference input/output files
                    FileInfo inputFile = new FileInfo(path);
                    FileInfo decryptedFile = new FileInfo(tempSecretDecryptedPath);

                    // Decrypt
                    PGP pgp = new PGP(encryptionKeys);
                    await pgp.DecryptFileAsync(inputFile, decryptedFile);
                }
            }

            // Sends a json of the file to the next logic app
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