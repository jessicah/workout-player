namespace BluetoothLE.Utilities
{
    public class SVGLineBuilder
    {
        public string Colour { get; init; }
        
        public int Height { get; init; }
        public int MaxValue { get; init; }

        public string Path = string.Empty;

        public double ScaleFactor { get; set; } = 4.0;

        private int _offset = 0;
        private bool _started = false;

        public double Width => _offset / ScaleFactor;

        public SVGLineBuilder(int height, string colour, int maxValue)
        {
            Height = height;
            Colour = colour;
            MaxValue = maxValue;

            Path += $"M0,{height}";
        }

        public void AddValue(int value)
        {
            double currentValue = ScaledValue(value);

            double startX = (double)(_offset) / ScaleFactor;
            double endX = (double)(_offset + 1) / ScaleFactor;

            if (_started is false)
            {
                Path += $"M{startX},{currentValue}";

                _started = true;
            }

            Path += $"L{endX},{currentValue}";

            ++_offset;
        }

        double ScaledValue(int value)
        {
            double clamped = Math.Min(value, MaxValue);
            double scaled = (clamped / (double)MaxValue) * Height;

            return Height - scaled;
        }
    }
}
