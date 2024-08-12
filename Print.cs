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
            string date = data?.Date;
            string time = data?.Time;

            if (type == "1") // Fi� yazd�rma
            {
                PrintReceipt(items, total, receiptNo, date, time);
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

        private void PrintReceipt(dynamic items, string total, string receiptNo, string date, string time)
        {
            PrintDocument printDocument = new PrintDocument();
            printDocument.PrinterSettings.PrinterName = receiptPrinterName;
            printDocument.PrintPage += (sender, e) =>
            {
                Graphics graphics = e.Graphics;
                float yPosition = -30; // �stten mesafe
                float margin = 10;
                Font font = new Font("Arial", 10);
                Font productFont = new Font("Arial", 9); // �r�n isimleri i�in daha k���k font
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
                                yPosition += businessNameSize.Height + 10; // Ba�l�k y�ksekli�i ve bo�luk

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
                string dates = $"Tarih : {date}";
                string times = $"Saat  : {time}";
                string receiptNumber = $"Fi� No : {receiptNo}"; // Fi� numaras�n� burada kullanaca��z

                // Tarih
                graphics.DrawString(dates, font, brush, margin, yPosition);

                // Saat, tarih hizas�nda sa�a yaslanm�� �ekilde
                SizeF timeSize = graphics.MeasureString(times, font);
                float timeX = e.PageBounds.Width - margin - timeSize.Width;
                graphics.DrawString(times, font, brush, timeX, yPosition);
                yPosition += font.GetHeight() + 5;

                // Fi� No
                graphics.DrawString(receiptNumber, font, brush, margin, yPosition);
                yPosition += font.GetHeight() + 10; // Fi� numaras�n�n alt�na bo�luk

                // Kesik �izgi ekleme
                using (Pen pen = new Pen(Color.Black))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; // Kesik �izgi stili
                    graphics.DrawLine(pen, margin, yPosition, e.PageBounds.Width - margin, yPosition);
                    yPosition += 20; // Kesik �izgiden sonraki bo�luk
                }

                // �r�n geni�likleri
                float itemNameWidth = e.PageBounds.Width - 2 * margin - 50; // �r�n ad� geni�li�i
                float quantityWidth = 70; // Adet geni�li�i
                float priceWidth = 40; // Fiyat geni�li�i

                foreach (dynamic item in items)
                {                    
                    string itemName = item.Name.ToString().ToUpper();
                    string itemQuantity = "x" + item.Quantity.ToString(); // Adedi buradan al
                    string itemPrice = "*" + item.Price.ToString("F2");
                    string truncatedItemName = TruncateText(itemName, itemNameWidth, font, e.Graphics);
                    // �r�n ad�n� ve ilgili bilgileri hizalama

                    // �r�n ad�n� �iz
                    graphics.DrawString(truncatedItemName, productFont, brush, margin, yPosition);

                    // Adet ve fiyat� ayn� sat�ra yazma
                    float itemNameEndX = margin + itemNameWidth;

                    // Adet ve fiyat hizalama
                    float quantityX = e.PageBounds.Width - margin - quantityWidth;
                    float priceX = e.PageBounds.Width - margin - priceWidth;
                   
                    graphics.DrawString(itemQuantity, productFont, brush, quantityX, yPosition);
                    graphics.DrawString(itemPrice, productFont, brush, priceX, yPosition);
                    
                    yPosition += productFont.GetHeight() + 5; // Sat�r y�ksekli�i                    
                }

                yPosition += 20; // Burada bo�luk ekliyoruz, ihtiyaca g�re art�r�labilir

                // Kesik �izgi ekleme
                using (Pen pen = new Pen(Color.Black))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; // Kesik �izgi stili
                    graphics.DrawLine(pen, margin, yPosition, e.PageBounds.Width - margin, yPosition);
                    yPosition += 20; // Kesik �izgiden sonraki bo�luk
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
            StringBuilder line = new StringBuilder();

            string[] words = text.Split(' ');

            foreach (string word in words)
            {
                // Mevcut sat�rdaki kelime eklenmeden �nce deneme
                string testLine = line.Length == 0 ? word : line + " " + word;
                SizeF size = TextRenderer.MeasureText(testLine, font);

                if (size.Width > maxWidth)
                {
                    // E�er test sat�r� geni�li�i s�n�r� a�arsa, mevcut sat�r� ekle
                    if (line.Length > 0)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }
                    // Yeni sat�ra ge�i�
                    line.Append(word);
                }
                else
                {
                    // E�er mevcut sat�ra s��arsa, kelimeyi ekle
                    line.Append(line.Length == 0 ? word : " " + word);
                }
            }

            // Son sat�r� da ekle
            if (line.Length > 0)
            {
                lines.Add(line.ToString());
            }

            return lines.ToArray();
        }

        private string TruncateText(string text, float maxWidth, Font font, Graphics graphics)
        {
            StringBuilder truncatedText = new StringBuilder();
            foreach (char c in text)
            {
                truncatedText.Append(c);
                SizeF textSize = graphics.MeasureString(truncatedText.ToString(), font);
                if (textSize.Width > maxWidth)
                {
                    truncatedText.Length--; // Son karakteri ��kart
                    break;
                }
            }
            return truncatedText.ToString() + "."; // Kesildi�ini belirten i�aret
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