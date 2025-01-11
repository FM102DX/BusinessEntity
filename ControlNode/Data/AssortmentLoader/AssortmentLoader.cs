using Newtonsoft.Json;
using ReactiveUI;
using SampleOnlineMall.Core;
using System.Reflection;
using SampleOnlineMall.DataAccess.DataAccess;
using SampleOnlineMall.WebLogger.Services;

namespace ControlNode.Data.AssortmentLoader
{
    public class AssortmentLoader : ReactiveObject
    {
        private string _status = "Idle";
        private string _content = "Idle";
        private int _loadedPos = 0;
        private Microsoft.Extensions.Options.IOptions<AppSettings> _settings;
        private readonly IWebHostEnvironment _env;
        private WebApiAsyncRepository<CommodityItemApiFeed> _webRepo;
        private IWebLoggerService _wLogger;
        public AssortmentLoader(
            Microsoft.Extensions.Options.IOptions<AppSettings> settings, 
            WebApiAsyncRepository<CommodityItemApiFeed> webRepo, 
            IWebHostEnvironment env,
            IWebLoggerService wLogger
            )
        {
            _settings=settings;
            _env = env;
            _webRepo = webRepo;
            _wLogger = wLogger;
        }
        public string Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value); 
        }
        public int LoadedPos
        {
            get => _loadedPos;
            set => this.RaiseAndSetIfChanged(ref _loadedPos, value);
        }

        public string Content
        {
            get => _content;
            set => this.RaiseAndSetIfChanged(ref _content, value);
        }

        public async Task<int> GetCountAsync()
        {
            return 0;
        }


        public async Task ClearAsync()
        {

        }

        public async Task LoadAsync()
        {
            //чистим ассортимент
           
            //берем ассортимент и перебираем там папки внутри
            LoadedPos = 0;
            List<CommodityItemApiFeed> resultFeedSource = new List<CommodityItemApiFeed>();

            // Получаем физический путь к wwwroot
            string wwwrootPath = Path.Combine(_env.WebRootPath, _settings.Value.AssortDataPath) ;

            if (Directory.Exists(wwwrootPath))
            {
                // Перебираем все каталоги внутри wwwroot
                var cats = Directory.GetDirectories(wwwrootPath, "*.*", SearchOption.TopDirectoryOnly).ToList();
                //Content = string.Join(";", cats);

                foreach (var folder in cats)
                {
                    var filePath = Path.Combine(folder, "item.json");
                    string jsonContent = File.ReadAllText(filePath);
                    var commodityItem = JsonConvert.DeserializeObject<CommodityItemApiFeed>(jsonContent);
                    if (commodityItem != null)
                    {
                        // Pic 1
                        string imagePath = Path.Combine(folder, "1.jpg");
                        if (File.Exists(imagePath))
                        {
                            byte[] bytesArr = File.ReadAllBytes(imagePath);
                            commodityItem.FirstPic = Convert.ToBase64String(bytesArr);
                        }

                        // Pic 2
                        imagePath = Path.Combine(folder, "2.jpg");
                        if (File.Exists(imagePath))
                        {
                            byte[] bytesArr = File.ReadAllBytes(imagePath);
                            commodityItem.SecondPic = Convert.ToBase64String(bytesArr);
                        }

                        // Pic 3
                        imagePath = Path.Combine(folder, "3.jpg");
                        if (File.Exists(imagePath))
                        {
                            byte[] bytesArr = File.ReadAllBytes(imagePath);
                            commodityItem.ThirdPic = Convert.ToBase64String(bytesArr);
                        }

                        resultFeedSource.Add(commodityItem);
                    }

                    break;
                }

                foreach (var item in resultFeedSource)
                {

                    _wLogger.Information($"ASSRTLDR_Sending assort item {item.Name}");
                    var rezult = await _webRepo.AddAsync(item);
                    
                    //await Task.Delay(1000);
                    LoadedPos++;
                }


            }
            else
            {
                Console.WriteLine("Каталог wwwroot не найден.");
            }

            //string executableDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //string[] files = Directory.GetFiles(executableDirectory);
            //Content = string.Join(";", files);
            
            Status = "Preparing";
            //await Task.Delay(500); // Асинхронная пауза
            //Status = "Loading";
            //await Task.Delay(500);
            //Status = "Completed";
            //return count;
        }
    }
}
