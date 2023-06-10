using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using System.Net.Http;
using System.Xml;
using Newtonsoft.Json;  // Change from 'System.Text.Json' to 'Newtonsoft.Json'
using Newtonsoft.Json.Linq;
using System.Net;

namespace TizenXamlApp1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public class Coin
    {
        public string Symbol { get; set; }
        public double PurchasePrice { get; set; }
        public double Quantity { get; set; }
        // 이전 거래내역 ( 현재가격 ) , 전일대비 두개는 변화시 깜빡이기
        public double PrevTradePrice { get; set; } = -1;

    }
    public class NewsResponse
    {
        public List<NewsItem> Items { get; set; }
    }

    public class NewsItem
    {
        public string Title { get; set; }
    }
    struct HandParams
    {
        public HandParams(double width, double height, double offset) : this()
        {
            Width = width;
            Height = height;
            Offset = offset;
        }
        public double Width { private set; get; }   // fraction of radius
        public double Height { private set; get; }  // ditto
        public double Offset { private set; get; }  // relative to center pivot
    }


    public partial class MainPage : ContentPage
    {
        private HttpClient client = new HttpClient();
        private Coin[] coins;
        public Dictionary<string, Dictionary<string, Label>> coinLabels;

        private List<string> newsTitles = new List<string>();
        private int currentTitleIndex = 0;
        BoxView[] tickMarks = new BoxView[60];

        // 아날로그 시계 객체바늘 객체
        static readonly HandParams secondParams = new HandParams(0.02, 1.1, 0.85);
        static readonly HandParams minuteParams = new HandParams(0.05, 0.8, 0.9);
        static readonly HandParams hourParams = new HandParams(0.125, 0.65, 0.9);
        private void InitializeCoinLabels()
        {
            coinLabels = new Dictionary<string, Dictionary<string, Label>>();

            for (int i = 0; i < coins.Length; i++)
            {
                var coinSymbol = coins[i].Symbol;
                totalPurchaseAmount += coins[i].PurchasePrice * coins[i].Quantity;
                coinLabels[coinSymbol] = new Dictionary<string, Label>
                {
                    ["코인심볼"] = (Label)this.FindByName($"코인심볼_{i + 1}"),
                    ["코인개수"] = (Label)this.FindByName($"코인개수_{i + 1}"),
                    ["코인수익률"] = (Label)this.FindByName($"코인수익률_{i + 1}"),
                    ["코인수익금"] = (Label)this.FindByName($"코인수익금_{i + 1}"),
                    ["코인평가금"] = (Label)this.FindByName($"코인평가금_{i + 1}"),
                    ["코인현재가"] = (Label)this.FindByName($"코인현재가_{i + 1}"),
                    ["전일대비상승금"] = (Label)this.FindByName($"코인전일대비상승금_{i + 1}"),
                    ["전일대비상승률"] = (Label)this.FindByName($"코인전일대비상승률_{i + 1}")
                };
            }
        }
        private double totalPurchaseAmount;
        private double totalEvaluation;
        private double totalProfitAmount;
        private double totalProfitRate;

        public MainPage()
        {
            InitializeComponent();
            for (int i = 0; i < tickMarks.Length; i++)
            {
                tickMarks[i] = new BoxView { Color = Color.Black };
                ClockLayout.Children.Add(tickMarks[i]);
            }
            Device.StartTimer(TimeSpan.FromSeconds(1.0 / 60), OnTimerTick);

            coins = new[]
            {
                new Coin { Symbol = "KRW-BTC", PurchasePrice = 30002000, Quantity = 0.1 },
                new Coin { Symbol = "KRW-ETH", PurchasePrice = 2582200, Quantity = 3},
                new Coin { Symbol = "KRW-STX", PurchasePrice = 500, Quantity = 1023},
            };
            InitializeCoinLabels();
            총매수금.Text = $"{totalPurchaseAmount:0}";
            Device.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                // Update the clock on the main thread
                Device.BeginInvokeOnMainThread(() =>
                {
                    DigitalClock.Text = DateTime.Now.ToString("HH:mm:ss");
                    


                });

                // List to hold all tasks
                var tasks = new List<Task>();

                for (int i = 0; i < coins.Length; i++)
                {
                    // Start each update task and add to the task list
                    tasks.Add(UpdateProfitRate(coins[i], i, coins[i].Symbol));
                }

                // Wait for all tasks to complete
                Task.WhenAll(tasks);
                총수익률.Text = $"{totalProfitRate:0.00}%";
                총수익금.Text = $"{totalProfitAmount:0}";
                총평가금.Text = $"{totalEvaluation:0}";
                totalProfitRate = 0;
                totalProfitAmount = 0;
                totalEvaluation = 0;
                return true; // returns true to repeat this timer
            });
            FetchNewsTitles();
            // 시계 바늘에 대한 애니메이션을 수행하는 메소드
            async Task UpdateProfitRate(Coin coin, int coinNum, string coinSymbol)
            {
                try
                {
                    string url = $"https://api.upbit.com/v1/ticker?markets={coin.Symbol}";

                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();

                    JArray jsonArr = JArray.Parse(responseBody);
                    JObject json = (JObject)jsonArr[0];
                    double tradePrice = (double)json["trade_price"];                // 가장최신거래가격
                    double prevClosingPrice = (double)json["prev_closing_price"];   // 전일종가

                    double purchaseAmount = coin.PurchasePrice * coin.Quantity;
                    double profitRate = (tradePrice - coin.PurchasePrice) / coin.PurchasePrice;
                    double profitAmount = profitRate * coin.PurchasePrice * coin.Quantity;
                    double evaluation = tradePrice * coin.Quantity;
                    double dayIncreaseAmount = tradePrice - prevClosingPrice;
                    double dayIncreaseRate = dayIncreaseAmount / prevClosingPrice * 100;
                    totalEvaluation += evaluation;
                    totalProfitAmount += profitAmount;
                    totalProfitRate = totalProfitAmount / totalPurchaseAmount * 100;

                    UpdateImages(coinNum, dayIncreaseRate);

                    // Get associated labels from dictionary
                    var labels = coinLabels[coinSymbol];
                    labels["코인심볼"].Text = coin.Symbol;
                    labels["코인개수"].Text = $"{coin.Quantity:0.00}개";
                    if (coin.PrevTradePrice != tradePrice)
                    {
                        coin.PrevTradePrice = tradePrice;

                        FadeAndUpdate(labels["코인현재가"], $"{tradePrice}");
                        FadeAndUpdate(labels["전일대비상승률"], $"{dayIncreaseRate:0.00}%");
                        FadeAndUpdate(labels["전일대비상승금"], $"{dayIncreaseAmount:0.00}");

                    }
                    labels["코인수익금"].Text = $"{profitAmount:0.00}";
                    labels["코인수익률"].Text = $"{profitRate * 100:0.00}%";
                    labels["코인평가금"].Text = $"{evaluation:0.00}";
                    SetProfitLabelColor(labels["전일대비상승률"], dayIncreaseRate);
                    SetProfitLabelColor(labels["전일대비상승금"], dayIncreaseAmount);

                    SetProfitLabelColor(labels["코인수익률"], profitRate);
                    SetProfitLabelColor(labels["코인수익금"], profitAmount);
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("\nException Caught!");
                    Console.WriteLine($"Message: {e.Message} ");
                }
            }
        }
        public async Task AddCoinAsync()
        {
            string symbol = "KRW-" + (await DisplayPromptAsync("New Coin", "Enter the coin symbol:")).ToUpper();
            string purchasePriceStr = await DisplayPromptAsync("New Coin", "Enter the purchase price:");
            string quantityStr = await DisplayPromptAsync("New Coin", "Enter the quantity:");

            double purchasePrice = double.Parse(purchasePriceStr);
            double quantity = double.Parse(quantityStr);
            if (!double.TryParse(quantityStr, out quantity))
            {
                await DisplayAlert("Error", "Please enter a valid number for the quantity.", "OK");
            }

            var newCoin = new Coin { Symbol = symbol, PurchasePrice = purchasePrice, Quantity = quantity };

            // Add new coin to the coins array
            var coinList = coins.ToList();
            coinList.Add(newCoin);
            coins = coinList.ToArray();

            // Initialize the labels for the new coin
            InitializeCoinLabels();
        }

        public async void OnAddCoinClicked(object sender, EventArgs args)
        {
            await AddCoinAsync();
        }

        async Task FadeAndUpdate(Label label, string newText)
        {
            // Fade out
            await label.FadeTo(0, 200);
            // Update the Label text
            label.Text = newText;
            // Fade in
            await label.FadeTo(1, 200);
        }

        

        public async void FetchNewsTitles()
        {
            var url = "https://openapi.naver.com/v1/search/news.json?query=" + Uri.EscapeDataString("암호화폐");
            client.DefaultRequestHeaders.Add("X-Naver-Client-Id", "9nDphUfYya60kdv43YOQ");
            client.DefaultRequestHeaders.Add("X-Naver-Client-Secret", "fyfZOVYPKD");

            HttpResponseMessage response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                var newsResponse = JsonConvert.DeserializeObject<NewsResponse>(content);

                foreach (var item in newsResponse.Items)
                {
                    newsTitles.Add(WebUtility.HtmlDecode(item.Title));
                }
            }
            else
            {
                throw new Exception("Failed to fetch news titles");
            }

            DisplayNextTitle();
        }

        public void DisplayNextTitle()
        {
            if (newsTitles.Count == 0) return;

            // Assuming you have a Label called NewsTitleLabel in your XAML for displaying news titles
            NEWS.Text = newsTitles[currentTitleIndex];
            MarqueeEffect(NEWS, 2000);

            currentTitleIndex = (currentTitleIndex + 1) % newsTitles.Count;
        }

        public async void MarqueeEffect(Label label, double translatePosition)
        {
            await label.TranslateTo(-translatePosition, 0, 8000);
            label.TranslationX = translatePosition;

            // Text 변경을 화면 밖에서 처리
            label.Text = newsTitles[currentTitleIndex];
            await label.TranslateTo(0, 0, 8000);

            DisplayNextTitle();  // Marquee 반복
        }

        private void SetProfitLabelColor(Label label, double value)
        {
            if (value >= 0)
            {
                label.TextColor = Color.Blue;
            }
            else
            {
                label.TextColor = Color.Red;
            }
        }
        // 아날로그 시계 사이즈 변경 method
        void OnAbsoluteLayoutSizeChanged(object sender, EventArgs args)
        {
            // Get the center and radius of the AbsoluteLayout.
            Point center = new Point(ClockLayout.Width / 2, ClockLayout.Height / 2);
            double radius = 0.45 * Math.Min(ClockLayout.Width, ClockLayout.Height);

            // Position, size, and rotate the 60 tick marks.
            for (int index = 0; index < tickMarks.Length; index++)
            {
                double size = radius / (index % 5 == 0 ? 15 : 30);
                double radians = index * 2 * Math.PI / tickMarks.Length;
                double x = center.X + radius * Math.Sin(radians) - size / 2;
                double y = center.Y - radius * Math.Cos(radians) - size / 2;
                AbsoluteLayout.SetLayoutBounds(tickMarks[index], new Rectangle(x, y, size, size));
                tickMarks[index].Rotation = 180 * radians / Math.PI;
            }

            // Position and size the three hands.
            LayoutHand(secondHand, secondParams, center, radius);
            LayoutHand(minuteHand, minuteParams, center, radius);
            LayoutHand(hourHand, hourParams, center, radius);
        }

        void LayoutHand(BoxView boxView, HandParams handParams, Point center, double radius)
        {
            double width = handParams.Width * radius;
            double height = handParams.Height * radius;
            double offset = handParams.Offset;

            AbsoluteLayout.SetLayoutBounds(boxView,
                new Rectangle(center.X - 0.5 * width,
                              center.Y - offset * height,
                              width, height));

            // Set the AnchorY property for rotations.
            boxView.AnchorY = handParams.Offset;
        }
        bool OnTimerTick()
        {
            // Set rotation angles for hour and minute hands.
            DateTime dateTime = DateTime.Now;
            hourHand.Rotation = 30 * (dateTime.Hour % 12) + 0.5 * dateTime.Minute;
            minuteHand.Rotation = 6 * dateTime.Minute + 0.1 * dateTime.Second;

            // Do an animation for the second hand.
            double t = dateTime.Millisecond / 1000.0;

            if (t < 0.5)
            {
                t = 0.5 * Easing.SpringIn.Ease(t / 0.5);
            }
            else
            {
                t = 0.5 * (1 + Easing.SpringOut.Ease((t - 0.5) / 0.5));
            }

            secondHand.Rotation = 6 * (dateTime.Second + t);
            return true;
        }

        // 수익화살표 회전에 대한 각도 정의
        double CalculateRotation(double value)
        {
            const double MAX_ANGLE = 80.0;
            // 상황에 따라 값의 범위를 수정할 수 있습니다. 
            // 예를 들어, 값이 -100과 100 사이라면, 각도는 -80과 80 사이가 될 것입니다.
            double normalizedValue = value / 100.0;
            return MAX_ANGLE * normalizedValue;
        }
        // 수익화살표 회전에 대한 Method 정의입니다.
        void UpdateImages(int coinNum, double dayIncreaseRate)
        {
            double rotation = CalculateRotation(dayIncreaseRate);

            Image imageToRotate;
            string imageSource;

            if (dayIncreaseRate >= 0)
            {
                imageSource = "inc.png";
                rotation = -rotation; // 음의 각도로 회전하여 inc.png가 반시계 방향으로 회전하게 합니다.
            }
            else
            {
                imageSource = "dec.png";
                // dec.png가 시계 방향으로 회전하도록 각도를 그대로 사용합니다.
            }

            // 선택한 코인에 따라 이미지를 선택합니다.
            switch (coinNum)
            {
                case 0:
                    imageToRotate = 코인이미지_1;
                    break;
                case 1:
                    imageToRotate = 코인이미지_2;
                    break;
                case 2:
                    imageToRotate = 코인이미지_3;
                    break;
                default:
                    throw new Exception("Invalid coin number");
            }

            // 이미지 소스와 회전을 업데이트합니다.
            imageToRotate.Source = imageSource;
            imageToRotate.Rotation = rotation;
        }
        // 코인심볼 누름에 따른 작동 Method
        void OnCoinSymbolTapped(object sender, EventArgs args)
        {
            var label = sender as Label;
            if (label != null)
            {
                string coinSymbol = label.Text;  // 코인심볼 텍스트 가져오기
                그래프.Source = $"https://www.upbit.com/full_chart?code=CRIX.UPBIT.KRW-{coinSymbol}";  // WebView URL 변경
            }
        }
    }
}


