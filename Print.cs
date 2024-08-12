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
        private string receiptPrinterName = "80mm Series Printer"; // Fiþ yazýcýsýnýn adý
        private string barcodePrinterName = "80mm Series Printer"; // Barkod yazýcýsýnýn adý

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

            if (type == "1") // Fiþ yazdýrma
            {
                PrintReceipt(items, total, receiptNo, date, time);
            }
            else if (type == "2") // Barkod yazdýrma
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
                float yPosition = -30; // Üstten mesafe
                float margin = 10;
                Font font = new Font("Arial", 10);
                Font productFont = new Font("Arial", 9); // Ürün isimleri için daha küçük font
                Brush brush = Brushes.Black;

                // Logo ekleme
                try
                {
                    string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "default.png");
                    if (File.Exists(logoPath))
                    {
                        using (Image originalLogo = Image.FromFile(logoPath))
                        {
                            // Logo boyutlarý
                            int newWidth = 175; // Ýstediðiniz geniþlik
                            int newHeight = (int)(originalLogo.Height * (newWidth / (float)originalLogo.Width)); // Orantýlý yükseklik

                            // Yeniden boyutlandýrýlmýþ logo oluþturma
                            using (Bitmap resizedLogo = new Bitmap(originalLogo, newWidth, newHeight))
                            {
                                float logoWidth = resizedLogo.Width;
                                float logoHeight = resizedLogo.Height;

                                // Resmi sayfanýn ortasýna konumlandýrma
                                float pageWidth = e.PageBounds.Width;
                                float logoX = (pageWidth - logoWidth) / 2;
                                float logoY = yPosition;

                                graphics.DrawImage(resizedLogo, logoX, logoY);

                                // Logonun altýna baþlýk ve adres ekleme
                                yPosition += logoHeight; // Logo yüksekliði ve boþluk

                                // Ýþletme adý
                                string businessName = "YILMAZ MARKET";
                                SizeF businessNameSize = graphics.MeasureString(businessName, new Font("Arial", 12, FontStyle.Bold));
                                float businessNameX = (e.PageBounds.Width - businessNameSize.Width) / 2; // Ortalamak için X konumu
                                graphics.DrawString(businessName, new Font("Arial", 12, FontStyle.Bold), brush, businessNameX, yPosition);
                                yPosition += businessNameSize.Height + 10; // Baþlýk yüksekliði ve boþluk

                                // Adres
                                string address = "ÇAY MAH.2059 SK. T4-A NO: 33 TEKKEKÖY/SAMSUN";
                                float addressWidth = e.PageBounds.Width - 2 * margin; // Adres geniþliði
                                string[] addressLines = WrapText(address, font, addressWidth);

                                // Adresi ortalama iþlemi
                                foreach (string line in addressLines)
                                {
                                    SizeF lineSize = graphics.MeasureString(line, font);
                                    float lineX = (e.PageBounds.Width - lineSize.Width) / 2; // Ortalamak için X konumu
                                    graphics.DrawString(line, font, brush, lineX, yPosition);
                                    yPosition += font.GetHeight() + 5; // Satýr yüksekliði
                                }

                                yPosition += 20; // Adresin altýna boþluk
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Logo dosyasý bulunamadý.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Logo yüklenirken bir hata oluþtu: {ex.Message}");
                }

                // Tarih, Saat ve Fiþ Numarasý
                DateTime now = DateTime.Now;
                string dates = $"Tarih : {date}";
                string times = $"Saat  : {time}";
                string receiptNumber = $"Fiþ No : {receiptNo}"; // Fiþ numarasýný burada kullanacaðýz

                // Tarih
                graphics.DrawString(dates, font, brush, margin, yPosition);

                // Saat, tarih hizasýnda saða yaslanmýþ þekilde
                SizeF timeSize = graphics.MeasureString(times, font);
                float timeX = e.PageBounds.Width - margin - timeSize.Width;
                graphics.DrawString(times, font, brush, timeX, yPosition);
                yPosition += font.GetHeight() + 5;

                // Fiþ No
                graphics.DrawString(receiptNumber, font, brush, margin, yPosition);
                yPosition += font.GetHeight() + 10; // Fiþ numarasýnýn altýna boþluk

                // Kesik çizgi ekleme
                using (Pen pen = new Pen(Color.Black))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; // Kesik çizgi stili
                    graphics.DrawLine(pen, margin, yPosition, e.PageBounds.Width - margin, yPosition);
                    yPosition += 20; // Kesik çizgiden sonraki boþluk
                }

                // Ürün geniþlikleri
                float itemNameWidth = e.PageBounds.Width - 2 * margin - 50; // Ürün adý geniþliði
                float quantityWidth = 70; // Adet geniþliði
                float priceWidth = 40; // Fiyat geniþliði

                foreach (dynamic item in items)
                {                    
                    string itemName = item.Name.ToString().ToUpper();
                    string itemQuantity = "x" + item.Quantity.ToString(); // Adedi buradan al
                    string itemPrice = "*" + item.Price.ToString("F2");
                    string truncatedItemName = TruncateText(itemName, itemNameWidth, font, e.Graphics);
                    // Ürün adýný ve ilgili bilgileri hizalama

                    // Ürün adýný çiz
                    graphics.DrawString(truncatedItemName, productFont, brush, margin, yPosition);

                    // Adet ve fiyatý ayný satýra yazma
                    float itemNameEndX = margin + itemNameWidth;

                    // Adet ve fiyat hizalama
                    float quantityX = e.PageBounds.Width - margin - quantityWidth;
                    float priceX = e.PageBounds.Width - margin - priceWidth;
                   
                    graphics.DrawString(itemQuantity, productFont, brush, quantityX, yPosition);
                    graphics.DrawString(itemPrice, productFont, brush, priceX, yPosition);
                    
                    yPosition += productFont.GetHeight() + 5; // Satýr yüksekliði                    
                }

                yPosition += 20; // Burada boþluk ekliyoruz, ihtiyaca göre artýrýlabilir

                // Kesik çizgi ekleme
                using (Pen pen = new Pen(Color.Black))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; // Kesik çizgi stili
                    graphics.DrawLine(pen, margin, yPosition, e.PageBounds.Width - margin, yPosition);
                    yPosition += 20; // Kesik çizgiden sonraki boþluk
                }

                // Fiþ alt kýsmý (toplam vs.)
                yPosition += 20;
                graphics.DrawString($"Toplam: {total} TL", new Font("Arial", 12, FontStyle.Bold), brush, margin, yPosition);
                yPosition += 20;

                // Alt çizgi
                graphics.DrawLine(Pens.Black, margin, yPosition, e.PageBounds.Width - margin, yPosition);
                yPosition += 20;

                // Ýmza vs.
                graphics.DrawString("Teþekkürler!", font, brush, margin, yPosition);

                e.HasMorePages = false;
            };

            try
            {
                printDocument.Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yazdýrma hatasý: {ex.Message}");
            }
        }

        private string[] WrapText(string text, Font font, float maxWidth)
        {
            List<string> lines = new List<string>();
            StringBuilder line = new StringBuilder();

            string[] words = text.Split(' ');

            foreach (string word in words)
            {
                // Mevcut satýrdaki kelime eklenmeden önce deneme
                string testLine = line.Length == 0 ? word : line + " " + word;
                SizeF size = TextRenderer.MeasureText(testLine, font);

                if (size.Width > maxWidth)
                {
                    // Eðer test satýrý geniþliði sýnýrý aþarsa, mevcut satýrý ekle
                    if (line.Length > 0)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }
                    // Yeni satýra geçiþ
                    line.Append(word);
                }
                else
                {
                    // Eðer mevcut satýra sýðarsa, kelimeyi ekle
                    line.Append(line.Length == 0 ? word : " " + word);
                }
            }

            // Son satýrý da ekle
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
                    truncatedText.Length--; // Son karakteri çýkart
                    break;
                }
            }
            return truncatedText.ToString() + "."; // Kesildiðini belirten iþaret
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
                    string code = item.Code; // `item`'ýn `Code` özelliðini `string` olarak alýn
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
                MessageBox.Show($"Yazdýrma hatasý: {ex.Message}");
            }
        }

        private Bitmap GenerateBarcode(string code)
        {
            var barcodeWriter = new BarcodeWriter<Bitmap>
            {
                Format = BarcodeFormat.CODE_128, // Barkod formatýný ayarlayýn
                Options = new EncodingOptions
                {
                    Width = 300, // Barkod geniþliði
                    Height = 150 // Barkod yüksekliði
                }
            };

            // Barkod resmi oluþtur
            Bitmap bitmap = barcodeWriter.Write(code);
            return bitmap;
        }
    }
}