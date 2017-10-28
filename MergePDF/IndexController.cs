using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Nancy;
using Newtonsoft.Json;

namespace MergePDF
{
    public class IndexController : Nancy.NancyModule
    {
        public IndexController()
        {
            Post["/"] = _ =>
            {
                string json = null;

                using (var reader = new StreamReader(Request.Body))
                {
                    json = reader.ReadToEnd();
                }

                var urls = JsonConvert.DeserializeObject<string[]>(json);
                var url = MergePDFUrls(urls);

                return Response.AsJson(new
                {
                    url = url

                });
            };

            Post["/images"] = _ =>
            {
                string json = null;

                using (var reader = new StreamReader(Request.Body))
                {
                    json = reader.ReadToEnd();
                }

                var urls = JsonConvert.DeserializeObject<string[]>(json);
                var url = MergeImages(urls);

                return Response.AsJson(new
                {
                    url = url
                });
            };

            Post["/imageTo4x6"] = _ =>
            {
                string json = null;

                using (var reader = new StreamReader(Request.Body))
                {
                    json = reader.ReadToEnd();
                }

                var urls = JsonConvert.DeserializeObject<string[]>(json);
                var url = Create4x6(urls.First());

                return Response.AsJson(new
                {
                    url = url
                });
            };
        }

        public static string Create4x6(string url)
        {
            var pdfpath = Path.GetTempFileName();
            Document doc = new Document();
            doc.SetPageSize(new Rectangle(4 * 72, 6 * 72));
            doc.SetMargins(0, 0, 0, 0);
            doc.PageCount = 1;


            try
            {
                PdfWriter.GetInstance(doc, new FileStream(pdfpath, FileMode.Create));
                doc.Open();

                Image png = Image.GetInstance(url);
                if(png.Width > png.Height)
                {
                    png.RotationDegrees = 90f;
                }
                
                png.ScalePercent((float)Math.Floor(doc.PageSize.Height / Math.Max(png.Height, png.Width) * 100)* 0.95f);

                //float yOffset = doc.PageSize.Height / 2 + 8;

                png.SetAbsolutePosition(5f, 0f);

                doc.Add(png);
            }
            catch (Exception ex)
            {
                return null;
                //Log error;
            }
            finally
            {
                doc.Close();
            }

            var util = new TransferUtility(RegionEndpoint.USWest2);
            var key = "batches/" + Guid.NewGuid() + ".pdf";
            util.Upload(new TransferUtilityUploadRequest()
            {
                BucketName = "cache.lulatools.net",
                Key = key,
                CannedACL = S3CannedACL.PublicRead,
                ContentType = "application/pdf",
                FilePath = pdfpath
            });

            File.Delete(pdfpath);

            return "http://s3-us-west-2.amazonaws.com/cache.lulatools.net/" + key;
        }

        public static string MergeImages(string[] imageURLs)
        {
            var pdfpath = Path.GetTempFileName();
            Document doc = new Document();
            doc.SetMargins(0, 0, 0, 0);
            doc.SetPageSize(PageSize.A4);
            doc.PageCount = imageURLs.Length / 2 + 1;
            int index = 0;


            try
            {
                PdfWriter.GetInstance(doc, new FileStream(pdfpath, FileMode.Create));
                doc.Open();

                foreach (var url in imageURLs)
                {
                    Image png = Image.GetInstance(url);

                    if (png.Width < png.Height)
                        png.RotationDegrees = 90f;

                    png.ScalePercent((float)Math.Floor(doc.PageSize.Width / Math.Max(png.Height, png.Width) * 100)* 0.95f);

                    float yOffset = doc.PageSize.Height / 2 + 8;

                    if (index % 2 == 1)
                        yOffset = 36;

                    png.SetAbsolutePosition((doc.PageSize.Width - png.ScaledWidth) / 2f, (doc.PageSize.Height / 2f - png.ScaledHeight) / 2f + yOffset);

                    doc.Add(png);

                    index++;

                    if(index % 2 == 0)
                        doc.NewPage();
                }
            }
            catch (Exception ex)
            {
                return null;
                //Log error;
            }
            finally
            {
                doc.Close();
            }

            var util = new TransferUtility(RegionEndpoint.USWest2);
            var key = "batches/" + Guid.NewGuid() + ".pdf";
            util.Upload(new TransferUtilityUploadRequest()
            {
                BucketName = "cache.lulatools.net",
                Key = key,
                CannedACL = S3CannedACL.PublicRead,
                ContentType = "application/pdf",
                FilePath = pdfpath
            });

            File.Delete(pdfpath);

            return "http://s3-us-west-2.amazonaws.com/cache.lulatools.net/" + key;

        }

        public static string MergePDFUrls(IEnumerable<string> urls)
        {
            List<Task<string>> tasks = new List<Task<string>>();
            using (var client = new HttpClient())
            {
                foreach(var url in urls)
                {
                    var task = client.GetByteArrayAsync(url).ContinueWith(c =>
                    { 
                        var file = Path.GetTempFileName();

                        Console.WriteLine(url + " -> " + file);

                        File.WriteAllBytes(file, c.Result);
                        return file;
                    });

                    tasks.Add(task);

                }

                Task.WaitAll(tasks.ToArray());
            }

            var files = tasks.Select(c => c.Result).ToArray();
            var destination = Path.GetTempFileName();

            Console.WriteLine($"Merging {files.Length} PDFs into " + destination);
            MergePDFs(files, destination);

            var util = new TransferUtility(RegionEndpoint.USWest2);
            var key = "batches/" + Guid.NewGuid() + ".pdf";
            util.Upload(new TransferUtilityUploadRequest()
            {
                BucketName = "cache.lulatools.net",
                Key = key,
                CannedACL = S3CannedACL.PublicRead,
                ContentType = "application/pdf",
                FilePath = destination
            });

            foreach(var file in files)
            {
                File.Delete(file);
            }

            File.Delete(destination);

            return "http://s3-us-west-2.amazonaws.com/cache.lulatools.net/" + key;
        }

        public static bool MergePDFs(IEnumerable<string> fileNames, string targetPdf)
        {
            bool merged = true;
            using (FileStream stream = new FileStream(targetPdf, FileMode.Create))
            {
                Document document = new Document();
                PdfCopy pdf = new PdfCopy(document, stream);
                PdfReader reader = null;
                try
                {
                    document.Open();
                    foreach (string file in fileNames)
                    {
                        reader = new PdfReader(file);
                        pdf.AddDocument(reader);
                        reader.Close();
                    }
                }
                catch (Exception)
                {
                    merged = false;
                    if (reader != null)
                    {
                        reader.Close();
                    }
                }
                finally
                {
                    if (document != null)
                    {
                        document.Close();
                    }
                }
            }
            return merged;
        }
    }
}
