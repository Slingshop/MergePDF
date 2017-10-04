# MergePDF
A simple server written in .NET that merges PDF and converts images to PDFs

/
Body: JSON-encoded array of strings. The strings should be absolute URLs of the target PDFs. 
Merges the list of PDFs into one PDF file
Returns: The URL of the uploaded PDF.

/images
Body: JSON-encoded array of strings.  The strings should be absolute URLs of the target shipping labels in image format. 
Merges all images passed in into a PDF File.
Returns: The URL of the uploaded PDF.
