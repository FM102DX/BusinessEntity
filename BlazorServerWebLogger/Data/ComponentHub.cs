using ReactiveUI;

namespace BlazorServerWebLogger.Data
{
    public class ComponentHub : ReactiveObject
    {
        private bool _isSampleLogsGenerationOn;

        public bool IsSampleLogsGenerationOn
        {
            get => _isSampleLogsGenerationOn;
            set => this.RaiseAndSetIfChanged(ref _isSampleLogsGenerationOn, value);
        }
    }
}
