namespace BluetoothLE
{
    public class StateContainer
    {
        public StateContainer()
        {
            Console.WriteLine("Created new state container");
        }

        private string? savedRefreshToken;
        private string? savedAccessToken;
        private DateTime? savedRefreshTime;

        public string RefreshToken
        {
            get => savedRefreshToken ?? string.Empty;
            set
            {
                savedRefreshToken = value;
                NotifyStateChanged();
            }
        }

        public string AccessToken
        {
            get => savedAccessToken ?? string.Empty;
            set
            {
                savedAccessToken = value;
                NotifyStateChanged();
            }
        }

        public DateTime? RefreshTime
        {
            get => savedRefreshTime;
            set
            {
                savedRefreshTime = value;
                NotifyStateChanged();
            }
        }

        public event Action? OnChange;

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
