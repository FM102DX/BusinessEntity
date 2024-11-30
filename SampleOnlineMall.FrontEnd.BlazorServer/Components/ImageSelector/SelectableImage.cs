namespace SampleOnlineMall.FrontEnd.BlazorServer.Components.ImageSelector
{
    public class SelectableImage
    {
        public int Id { get; set; }
        public string FullSizePath { get; set; }
        public string MidSizePath { get; set; }
        public string ThumbPath { get; set; }
        public bool Selected { get; set; }

    }
}
