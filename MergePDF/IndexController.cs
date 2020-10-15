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
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;
using iTextSharp.tool.xml;
using Nancy;
using Newtonsoft.Json;

namespace MergePDF
{
    public class IndexController : Nancy.NancyModule
    {
        public IndexController()
        {
            Get["/"] = _ =>
            {
	            var r = (Response)"Response";
	            r.StatusCode = HttpStatusCode.OK;

	            return r;
            };

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

            Post["/html"] = _ =>
            {
                string json = null;
                
                using (var reader = new StreamReader(Request.Body))
                {
                    json = reader.ReadToEnd();
                }

                var html = JsonConvert.DeserializeObject<string>(json);

                var url = MergeHtml(html);

                return Response.AsJson(new
                {
                    url = url

                });
            };

            Post["/manifest"] = _ =>
            {
                string json = null;

                using (var reader = new StreamReader(Request.Body))
                {
                    json = reader.ReadToEnd();
                }

                var urls = JsonConvert.DeserializeObject<string[]>(json);
                var url = MergePDFUrls(urls, 0.24f);

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
                var url = CreateLabel(urls.First(), 4, 6, 5, 0, 0.95f);

                return Response.AsJson(new
                {
                    url = url
                });
            };

            Post["/imageTo2point3x4"] = _ =>
            {
                string json = null;

                using (var reader = new StreamReader(Request.Body))
                {
                    json = reader.ReadToEnd();
                }

                var urls = JsonConvert.DeserializeObject<string[]>(json);
                var url = CreateLabel(urls.First(), 2.3125m, 4, 6f, 10, .91f);

                return Response.AsJson(new
                {
                    url = url
                });
            };
        }

        private static string CreateLabel(string url, decimal x, decimal y, float absoluteX, float absoluteY, float scale)
        {
            var pdfpath = Path.GetTempFileName();
            Document doc = new Document();
            doc.SetPageSize(new Rectangle((float)(x * 72), (float)(y * 72)));
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
                
                png.ScalePercent((float)Math.Floor(doc.PageSize.Height / Math.Max(png.Height, png.Width) * 100)* scale);

                //float yOffset = doc.PageSize.Height / 2 + 8;

                png.SetAbsolutePosition(absoluteX, absoluteY);

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
        
        private static string MergeImages(string[] imageURLs)
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

                    png.ScalePercent((float)Math.Floor(doc.PageSize.Width / Math.Max(png.Height, png.Width) * 100)* 0.85f);

                    float yOffset = doc.PageSize.Height / 2 + 10;

                    if (index % 2 == 1)
                        yOffset = 35;

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

        private static string MergePDFUrls(IEnumerable<string> urls, float? shrinkScale = null)
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
            if (shrinkScale.HasValue)
            {
                ShrinkToFit(files, destination, shrinkScale.Value);
            }
            else
            {
                MergePDFs(files, destination);
            }

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

        private static string MergeHtml(string html)
        {

            Document document = new Document();
            var destination = Path.GetTempFileName();

            using (FileStream stream = new FileStream(destination, FileMode.Create))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(html);

                using (var input = new MemoryStream(bytes))
                {
                    document = new iTextSharp.text.Document(iTextSharp.text.PageSize.LETTER, 50, 50, 50, 50);
                    var writer = PdfWriter.GetInstance(document, stream);
                    writer.CloseStream = false;
                    document.Open();

                    var xmlWorker = XMLWorkerHelper.GetInstance();
                    xmlWorker.ParseXHtml(writer, document, input, null, Encoding.UTF8);
                    document.Close();
                }

            }

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

            File.Delete(destination);

            return "http://s3-us-west-2.amazonaws.com/cache.lulatools.net/" + key;
        }

        private static bool MergePDFs(IEnumerable<string> fileNames, string targetPdf)
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
                        PdfReader.unethicalreading = true;
                        pdf.AddDocument(reader);
                        reader.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
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

        private static bool ShrinkToFit(IEnumerable<string> fileNames, string targetPdf, float scale)
        {
            PdfReader reader = new PdfReader(fileNames.First());
            Rectangle pagesize = reader.GetPageSize(1);
            if (pagesize.Width != 2550)
            {
                scale = 1;
            }
            Document doc = new Document(PageSize.A4, 0, 0, 0, 0);
            PdfWriter writer = PdfWriter.GetInstance(doc, new FileStream(targetPdf, FileMode.Create));
            doc.Open();
            PdfContentByte cb = writer.DirectContent;
            PdfImportedPage page = writer.GetImportedPage(reader, 1); //page #1
            cb.AddTemplate(page, scale, 0, 0, scale, 0, 0);
            doc.Close();
            reader.Close();
            writer.Close();
            return true;
        }
    }
}
