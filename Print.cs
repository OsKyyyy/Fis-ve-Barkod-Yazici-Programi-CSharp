using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Imaging;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.Json;
using ZXing;
using ZXing.QrCode;
using ZXing.Common;

namespace PrintApp
{
    public partial class Print : Form
    {
        private string receiptPrinterName = "80mm Series Printer"; // Fi� yaz�c�s�n�n ad�
        private string barcodePrinterName = "80mm Series Printer"; // Barkod yaz�c�s�n�n ad�

        public Print()
        {
            InitializeComponent();
            StartListening();
        }

        private void StartListening()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/");
            listener.Start();
            listener.BeginGetContext(new AsyncCallback(ProcessRequest), listener);
        }

        private void ProcessRequest(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            HttpListenerContext context = listener.EndGetContext(result);

            HttpListenerRequest request = context.Request;
            string body;
            using (var reader = new StreamReader(request.InputStream))
            {
                body = reader.ReadToEnd();
            }

            dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(body);

            string type = data?.type;
            var items = data?.Items;
            string total = data?.Total;
            string receiptNo = data?.ReceiptNo;

            if (type == "1") // Fi� yazd�rma
            {
                PrintReceipt(items, total, receiptNo);
            }
            else if (type == "2") // Barkod yazd�rma
            {
                PrintBarcode(items);
            }

            HttpListenerResponse response = context.Response;
            string responseString = "Success";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            using (var output = response.OutputStream)
            {
                output.Write(buffer, 0, buffer.Length);
            }
        }

        private void PrintReceipt(dynamic items, string total, string receiptNo)
        {
            PrintDocument printDocument = new PrintDocument();
            printDocument.PrinterSettings.PrinterName = receiptPrinterName;
            printDocument.PrintPage += (sender, e) =>
            {
                Graphics graphics = e.Graphics;
                float yPosition = -30;
                float margin = 10;
                Font font = new Font("Arial", 10);
                Brush brush = Brushes.Black;

                // Logo ekleme
                try
                {
                    string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "default.png");
                    if (File.Exists(logoPath))
                    {
                        using (Image originalLogo = Image.FromFile(logoPath))
                        {
                            // Logo boyutlar�
                            int newWidth = 175; // �stedi�iniz geni�lik
                            int newHeight = (int)(originalLogo.Height * (newWidth / (float)originalLogo.Width)); // Orant�l� y�kseklik

                            // Yeniden boyutland�r�lm�� logo olu�turma
                            using (Bitmap resizedLogo = new Bitmap(originalLogo, newWidth, newHeight))
                            {
                                float logoWidth = resizedLogo.Width;
                                float logoHeight = resizedLogo.Height;

                                // Resmi sayfan�n ortas�na konumland�rma
                                float pageWidth = e.PageBounds.Width;
                                float logoX = (pageWidth - logoWidth) / 2;
                                float logoY = yPosition;

                                graphics.DrawImage(resizedLogo, logoX, logoY);

                                // Logonun alt�na ba�l�k ve adres ekleme
                                yPosition += logoHeight; // Logo y�ksekli�i ve bo�luk

                                // ��letme ad�
                                string businessName = "YILMAZ MARKET";
                                SizeF businessNameSize = graphics.MeasureString(businessName, new Font("Arial", 12, FontStyle.Bold));
                                float businessNameX = (e.PageBounds.Width - businessNameSize.Width) / 2; // Ortalamak i�in X konumu
                                graphics.DrawString(businessName, new Font("Arial", 12, FontStyle.Bold), brush, businessNameX, yPosition);
                                yPosition += 10; // Ba�l�k y�ksekli�i

                                // Adres
                                string address = "�AY MAH.2059 SK. T4-A NO: 33 TEKKEK�Y/SAMSUN";
                                float addressWidth = e.PageBounds.Width - 2 * margin; // Adres geni�li�i
                                string[] addressLines = WrapText(address, font, addressWidth);

                                // Adresi ortalama i�lemi
                                foreach (string line in addressLines)
                                {
                                    SizeF lineSize = graphics.MeasureString(line, font);
                                    float lineX = (e.PageBounds.Width - lineSize.Width) / 2; // Ortalamak i�in X konumu
                                    graphics.DrawString(line, font, brush, lineX, yPosition);
                                    yPosition += font.GetHeight() + 5; // Sat�r y�ksekli�i
                                }

                                yPosition += 20; // Adresin alt�na bo�luk
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Logo dosyas� bulunamad�.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Logo y�klenirken bir hata olu�tu: {ex.Message}");
                }

                // Tarih, Saat ve Fi� Numaras�
                DateTime now = DateTime.Now;
                string date = $"Tarih : {now:dd/MM/yyyy}";
                string time = $"Saat  : {now:HH:mm}";
                string receiptNumber = $"Fi� No : {receiptNo}"; // Fi� numaras�n� burada kullanaca��z

                // Tarih
                graphics.DrawString(date, font, brush, margin, yPosition);
                yPosition += font.GetHeight() + 5;

                // Saat
                graphics.DrawString(time, font, brush, margin, yPosition);
                yPosition += font.GetHeight() + 5;

                // Fi� No
                graphics.DrawString(receiptNumber, font, brush, margin, yPosition);
                yPosition += font.GetHeight() + 10; // Fi� numaras�n�n alt�na bo�luk

                // Ba�l�k
                graphics.DrawString("Market Fi�i", new Font("Arial", 12, FontStyle.Bold), brush, margin, yPosition);
                yPosition += 20;

                // �r�nlerin yazd�r�lmas�
                foreach (object item in items)
                {
                    string itemText = item.ToString();  // Object t�r�n� string'e d�n��t�rme
                    graphics.DrawString(itemText, font, brush, margin, yPosition);
                    yPosition += font.GetHeight() + 5;
                }

                // Fi� alt k�sm� (toplam vs.)
                yPosition += 20;
                graphics.DrawString($"Toplam: {total} TL", new Font("Arial", 12, FontStyle.Bold), brush, margin, yPosition);
                yPosition += 20;

                // Alt �izgi
                graphics.DrawLine(Pens.Black, margin, yPosition, e.PageBounds.Width - margin, yPosition);
                yPosition += 20;

                // �mza vs.
                graphics.DrawString("Te�ekk�rler!", font, brush, margin, yPosition);

                e.HasMorePages = false;
            };

            try
            {
                printDocument.Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yazd�rma hatas�: {ex.Message}");
            }
        }

        private string[] WrapText(string text, Font font, float maxWidth)
        {
            List<string> lines = new List<string>();
            string[] words = text.Split(' ');
            StringBuilder line = new StringBuilder();

            foreach (string word in words)
            {
                string testLine = line.Length == 0 ? word : line + " " + word;
                SizeF size = TextRenderer.MeasureText(testLine, font);
                if (size.Width > maxWidth)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                    line.Append(word);
                }
                else
                {
                    line.Append(word);
                }
            }

            if (line.Length > 0)
            {
                lines.Add(line.ToString());
            }

            return lines.ToArray();
        }

        private void PrintBarcode(dynamic items)
        {
            PrintDocument printDocument = new PrintDocument();
            printDocument.PrinterSettings.PrinterName = barcodePrinterName;
            printDocument.PrintPage += (sender, e) =>
            {
                float yPosition = 0;
                float margin = 10;

                foreach (var item in items)
                {
                    string code = item.Code; // `item`'�n `Code` �zelli�ini `string` olarak al�n
                    Bitmap barcodeImage = GenerateBarcode(code);
                    e.Graphics.DrawImage(barcodeImage, margin, yPosition);
                    yPosition += barcodeImage.Height + 10;
                }

                e.HasMorePages = false;
            };

            try
            {
                printDocument.Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yazd�rma hatas�: {ex.Message}");
            }
        }

        private Bitmap GenerateBarcode(string code)
        {
            var barcodeWriter = new BarcodeWriter<Bitmap>
            {
                Format = BarcodeFormat.CODE_128, // Barkod format�n� ayarlay�n
                Options = new EncodingOptions
                {
                    Width = 300, // Barkod geni�li�i
                    Height = 150 // Barkod y�ksekli�i
                }
            };

            // Barkod resmi olu�tur
            Bitmap bitmap = barcodeWriter.Write(code);
            return bitmap;
        }
    }
}