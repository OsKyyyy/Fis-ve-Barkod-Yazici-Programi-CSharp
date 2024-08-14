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
using ZXing.Rendering;
using ZXing.Windows.Compatibility;
using ZXing.QrCode.Internal;

namespace PrintApp
{
    public partial class Print : Form
    {
        private string receiptPrinterName = "80mm Series Printer"; // Fiş yazıcısının adı
        private string barcodePrinterName = "80mm Series Printer"; // Barkod yazıcısının adı

        public Print()
        {
            InitializeComponent();
            // Formu gizle
            this.Load += (sender, e) =>
            {
                this.WindowState = FormWindowState.Minimized; // Formu simge durumuna küçült
                this.ShowInTaskbar = false; // Görev çubuğunda gösterme
                this.Hide(); // Formu gizle
            };
            StartListeningAsync();
        }

        private async Task StartListeningAsync()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/");
            listener.Start();

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                _ = ProcessRequestAsync(context); // ProcessRequestAsync'i çağırıyoruz ve görev sonucu ile ilgilenmiyoruz
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            string body;
            using (var reader = new StreamReader(request.InputStream))
            {
                body = await reader.ReadToEndAsync();
            }

            dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(body);

            string type = data?.Type;

            var items = data?.Items;
            string total = data?.Total;
            string receiptNo = data?.ReceiptNo;
            string date = data?.Date;
            string time = data?.Time;

            string code = data?.Code;
            string productName = data?.ProductName;
            string productPrice = data?.ProductPrice;
            string priceChangeDate = data?.PriceChangeDate;

            try
            {
                if (type == "1") // Fiş yazdırma
                {
                    PrintReceipt(items, total, receiptNo, date, time);
                }
                else if (type == "2") // Barkod yazdırma
                {
                    PrintBarcode(code, productName, productPrice, priceChangeDate);
                }

                // JSON yanıtı hazırlama
                var responseObject = new
                {
                    status = true,
                    message = "İşlem başarıyla tamamlandı."
                };

                // Yanıtı JSON formatında döndürme
                HttpListenerResponse response = context.Response;
                string responseString = Newtonsoft.Json.JsonConvert.SerializeObject(responseObject);
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "application/json"; // JSON içerik tipi
                using (var output = response.OutputStream)
                {
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                // Hata durumunda JSON yanıtı hazırlama
                var errorResponseObject = new
                {
                    status = false,
                    message = $"Bir hata oluştu: {ex.Message}"
                };

                HttpListenerResponse response = context.Response;
                string responseString = Newtonsoft.Json.JsonConvert.SerializeObject(errorResponseObject);
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "application/json"; // JSON içerik tipi
                using (var output = response.OutputStream)
                {
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }
            }
        }

        private void SendResponse(HttpListenerResponse response, string responseString)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            using (var output = response.OutputStream)
            {
                output.Write(buffer, 0, buffer.Length);
            }
        }

        private void LogError(string message)
        {
            File.AppendAllText("error_log.txt", $"{DateTime.Now}: {message}\n");
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
                Font infoFont = new Font("Arial", 8);
                Font productFont = new Font("Arial", 9); // Ürün isimleri için daha küçük font
                Font headFont = new Font("Arial", 12, FontStyle.Bold); // Başlık için font
                Brush brush = Brushes.Black;

                // Logo ekleme
                try
                {
                    string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "default.png");
                    if (File.Exists(logoPath))
                    {
                        using (Image originalLogo = Image.FromFile(logoPath))
                        {
                            // Logo boyutları
                            int newWidth = 175; // İstediğiniz genişlik
                            int newHeight = (int)(originalLogo.Height * (newWidth / (float)originalLogo.Width)); // Orantılı yükseklik

                            // Yeniden boyutlandırılmış logo oluşturma
                            using (Bitmap resizedLogo = new Bitmap(originalLogo, newWidth, newHeight))
                            {
                                float logoWidth = resizedLogo.Width;
                                float logoHeight = resizedLogo.Height;

                                // Resmi sayfanın ortasına konumlandırma
                                float pageWidth = e.PageBounds.Width;
                                float logoX = (pageWidth - logoWidth) / 2;
                                float logoY = yPosition;

                                graphics.DrawImage(resizedLogo, logoX, logoY);

                                // Logonun altına başlık ve adres ekleme
                                yPosition += logoHeight; // Logo yüksekliği ve boşluk

                                // İşletme adı
                                string businessName = "YILMAZ MARKET";
                                SizeF businessNameSize = graphics.MeasureString(businessName, new Font("Arial", 12, FontStyle.Bold));
                                float businessNameX = (e.PageBounds.Width - businessNameSize.Width) / 2; // Ortalamak için X konumu
                                graphics.DrawString(businessName, new Font("Arial", 12, FontStyle.Bold), brush, businessNameX, yPosition);
                                yPosition += businessNameSize.Height + 10; // Başlık yüksekliği ve boşluk

                                // Adres
                                string address = "ÇAY MAH.2059 SK. T4-A NO: 33 TEKKEKÖY/SAMSUN";
                                float addressWidth = e.PageBounds.Width - 2 * margin; // Adres genişliği
                                string[] addressLines = WrapText(address, font, addressWidth);

                                // Adresi ortalama işlemi
                                foreach (string line in addressLines)
                                {
                                    SizeF lineSize = graphics.MeasureString(line, font);
                                    float lineX = (e.PageBounds.Width - lineSize.Width) / 2; // Ortalamak için X konumu
                                    graphics.DrawString(line, font, brush, lineX, yPosition);
                                    yPosition += font.GetHeight() + 5; // Satır yüksekliği
                                }

                                yPosition += 10; // Adresin altına boşluk
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Logo dosyası bulunamadı.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Logo yüklenirken bir hata oluştu: {ex.Message}");
                }

                // "BİLGİ FİŞİ" yazısını ekleme
                string infoText = "BİLGİ FİŞİ";
                SizeF infoTextSize = graphics.MeasureString(infoText, headFont);
                float infoTextX = (e.PageBounds.Width - infoTextSize.Width) / 2; // Ortalamak için X konumu
                graphics.DrawString(infoText, headFont, brush, infoTextX, yPosition);
                yPosition += infoTextSize.Height + 10; // "BİLGİ FİŞİ" yazısından sonra boşluk

                // Tarih, Saat ve Fiş Numarası
                DateTime now = DateTime.Now;
                string dates = $"Tarih : {date}";
                string times = $"Saat  : {time}";
                string receiptNumber = $"Fiş No : {receiptNo}"; // Fiş numarasını burada kullanacağız

                // Tarih
                graphics.DrawString(dates, font, brush, margin, yPosition);

                // Saat, tarih hizasında sağa yaslanmış şekilde
                SizeF timeSize = graphics.MeasureString(times, font);
                float timeX = e.PageBounds.Width - margin - timeSize.Width;
                graphics.DrawString(times, font, brush, timeX, yPosition);
                yPosition += font.GetHeight() + 5;

                // Fiş No
                graphics.DrawString(receiptNumber, font, brush, margin, yPosition);
                yPosition += font.GetHeight() + 10; // Fiş numarasının altına boşluk

                // Kesik çizgi ekleme
                using (Pen pen = new Pen(Color.Black))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; // Kesik çizgi stili
                    graphics.DrawLine(pen, margin, yPosition, e.PageBounds.Width - margin, yPosition);
                    yPosition += 20; // Kesik çizgiden sonraki boşluk
                }

                // Ürün genişlikleri
                float itemNameWidth = e.PageBounds.Width - 2 * margin - 50; // Ürün adı genişliği
                float quantityWidth = 70; // Adet genişliği
                float priceWidth = 40; // Fiyat genişliği

                foreach (dynamic item in items)
                {                    
                    string itemName = item.Name.ToString().ToUpper();
                    string itemQuantity = "x" + item.Quantity.ToString(); // Adedi buradan al
                    string itemPrice = "*" + item.Price.ToString("F2");
                    string truncatedItemName = TruncateText(itemName, itemNameWidth, font, e.Graphics);
                    // Ürün adını ve ilgili bilgileri hizalama

                    // Ürün adını çiz
                    graphics.DrawString(truncatedItemName, productFont, brush, margin, yPosition);

                    // Adet ve fiyatı aynı satıra yazma
                    float itemNameEndX = margin + itemNameWidth;

                    // Adet ve fiyat hizalama
                    float quantityX = e.PageBounds.Width - margin - quantityWidth;
                    float priceX = e.PageBounds.Width - margin - priceWidth;
                   
                    graphics.DrawString(itemQuantity, productFont, brush, quantityX, yPosition);
                    graphics.DrawString(itemPrice, productFont, brush, priceX, yPosition);
                    
                    yPosition += productFont.GetHeight() + 5; // Satır yüksekliği                    
                }

                yPosition += 20; // Burada boşluk ekliyoruz, ihtiyaca göre artırılabilir

                // Kesik çizgi ekleme
                using (Pen pen = new Pen(Color.Black))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; // Kesik çizgi stili
                    graphics.DrawLine(pen, margin, yPosition, e.PageBounds.Width - margin, yPosition);
                    yPosition += 20; // Kesik çizgiden sonraki boşluk
                }

                // TOPLAM ve Toplam Tutarı yazma
                string totalLabel = "TOPLAM";
                string totalAmount = $"{total} TL";
                SizeF totalLabelSize = graphics.MeasureString(totalLabel, headFont);
                SizeF totalAmountSize = graphics.MeasureString(totalAmount, headFont);

                // "TOPLAM" yazısını sola hizalama
                graphics.DrawString(totalLabel, headFont, brush, margin, yPosition);

                // Toplam tutarı sağa hizalama
                float totalAmountX = e.PageBounds.Width - margin - totalAmountSize.Width;
                graphics.DrawString(totalAmount, headFont, brush, totalAmountX, yPosition);
                yPosition += headFont.GetHeight() + 20; // Toplam tutarın altına boşluk

                // Alt çizgi
                graphics.DrawLine(Pens.Black, margin, yPosition, e.PageBounds.Width - margin, yPosition);
                yPosition += 20;

                // TEŞEKKÜRLER yazısını ekleme
                string thankYouText = "TEŞEKKÜRLER";
                SizeF thankYouSize = graphics.MeasureString(thankYouText, headFont);
                float thankYouX = (e.PageBounds.Width - thankYouSize.Width) / 2;
                graphics.DrawString(thankYouText, headFont, brush, thankYouX, yPosition);
                yPosition += headFont.GetHeight() + 5;

                // Yine Bekleriz yazısını ekleme
                string againText = "iyi günler, yine bekleriz";
                SizeF againSize = graphics.MeasureString(againText, font);
                float againX = (e.PageBounds.Width - againSize.Width) / 2;
                graphics.DrawString(againText, font, brush, againX, yPosition);
                yPosition += headFont.GetHeight() + 20;

                // Bilgi yazısını ekleme
                string infoTextFooter = "*** Bilgi Fişidir. Mali Değeri Yok. ***";
                SizeF infoSize = graphics.MeasureString(infoTextFooter, font);
                float infoX = (e.MarginBounds.Width - infoSize.Width) / 2 + e.MarginBounds.Left;
                graphics.DrawString(infoTextFooter, font, brush, infoX, yPosition);
                yPosition += font.GetHeight() + 20;

                string test = "###";
                SizeF testSize = graphics.MeasureString(test, font);
                float testX = (e.MarginBounds.Width - testSize.Width) / 2 + e.MarginBounds.Left;
                graphics.DrawString(test, font, brush, testX, yPosition);

                e.HasMorePages = false;
            };

            try
            {
                printDocument.Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yazdırma hatası: {ex.Message}");
            }
        }

        private string[] WrapText(string text, Font font, float maxWidth)
        {
            List<string> lines = new List<string>();
            StringBuilder line = new StringBuilder();

            string[] words = text.Split(' ');

            foreach (string word in words)
            {
                // Mevcut satırdaki kelime eklenmeden önce deneme
                string testLine = line.Length == 0 ? word : line + " " + word;
                SizeF size = TextRenderer.MeasureText(testLine, font);

                if (size.Width > maxWidth)
                {
                    // Eğer test satırı genişliği sınırı aşarsa, mevcut satırı ekle
                    if (line.Length > 0)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }
                    // Yeni satıra geçiş
                    line.Append(word);
                }
                else
                {
                    // Eğer mevcut satıra sığarsa, kelimeyi ekle
                    line.Append(line.Length == 0 ? word : " " + word);
                }
            }

            // Son satırı da ekle
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
                    truncatedText.Length--; // Son karakteri çıkart
                    break;
                }
            }
            return truncatedText.ToString() + "."; // Kesildiğini belirten işaret
        }

        private void PrintBarcode(string code, string productName, string productPrice, string priceChangeDate)
        {
            PrintDocument printDocument = new PrintDocument();
            printDocument.PrinterSettings.PrinterName = barcodePrinterName;
            printDocument.PrintPage += (sender, e) =>
            {
                float margin = 10;
                float yPosition = margin;
                float maxWidth = e.PageBounds.Width - 2 * margin;
                float lineHeight = 20; // Satır yüksekliği
                Font priceFont = new Font("Arial", 20, FontStyle.Bold);
                Font boldFont = new Font("Arial", 12, FontStyle.Bold);
                Font regularFont = new Font("Arial", 10);
                Font smallFont = new Font("Arial", 8);
                Brush brush = Brushes.Black;

                // Ürün adını çok satıra bölme
                string[] productNameLines = WrapText(productName, boldFont, maxWidth);

                // Ürün adı çizimi
                foreach (string line in productNameLines)
                {
                    e.Graphics.DrawString(line, boldFont, brush, margin, yPosition);
                    yPosition += lineHeight;
                }

                // Fiyat değişiklik tarihi
                yPosition += 30; // Ürün adı ve fiyat değişiklik tarihi arasına boşluk bırak
                e.Graphics.DrawString($"Fiyat Değişiklik Tarihi : {priceChangeDate}", smallFont, brush, margin, yPosition);
                yPosition += lineHeight;

                // Barkod resmi
                Bitmap barcodeImage = GenerateBarcode(code);
                e.Graphics.DrawImage(barcodeImage, margin, yPosition);

                // Ürün fiyatını barkodun sağ tarafına hizalamak için
                //float priceXPosition = margin + 160;
                //e.Graphics.DrawString(productPrice + " ₺", priceFont, Brushes.Black, priceXPosition, yPosition);

                //// "KDV Dahil" yazısı
                //string kdvText = "KDV Dahil";
                //SizeF kdvTextSize = e.Graphics.MeasureString(kdvText, smallFont);
                //float kdvXPosition = priceXPosition + e.Graphics.MeasureString(productPrice, regularFont).Width;
                //float kdvYPosition = yPosition + lineHeight + 15; // Fiyatın hemen altına hizala
                //e.Graphics.DrawString(kdvText, smallFont, Brushes.Black, kdvXPosition, kdvYPosition);

                // Fiyat ve "KDV Dahil" yazısı
                SizeF priceSize = e.Graphics.MeasureString(productPrice, priceFont);
                SizeF kdvTextSize = e.Graphics.MeasureString("KDV Dahil", smallFont);

                // Fiyatın sağ kenarı için hesaplama
                float priceXPosition = margin + 150;
                float kdvXPosition = priceXPosition + priceSize.Width - kdvTextSize.Width + 10;
                float priceYPosition = yPosition;
                float kdvYPosition = yPosition + priceSize.Height;

                e.Graphics.DrawString(productPrice + " ₺", priceFont, Brushes.Black, priceXPosition, priceYPosition);
                e.Graphics.DrawString("KDV Dahil", smallFont, Brushes.Black, kdvXPosition, kdvYPosition);

                // Barkod yüksekliği + küçük bir boşluk
                yPosition += barcodeImage.Height + 10;

                e.HasMorePages = false;
            };

            try
            {
                printDocument.Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yazdırma hatası: {ex.Message}");
            }
        }

        private Bitmap GenerateBarcode(string barcodeText)
        {
            BarcodeWriter<Bitmap> barcodeWriter = new BarcodeWriter<Bitmap>
            {
                Format = BarcodeFormat.CODE_128, // Barkod formatı
                Options = new EncodingOptions
                {
                    Width = 50,
                    Height = 50
                },
                Renderer = new BitmapRenderer() // Renderer ayarları
            };

            // Barkod resmi oluşturma
            Bitmap barcodeBitmap = barcodeWriter.Write(barcodeText);
            return barcodeBitmap;
        }
    }
}