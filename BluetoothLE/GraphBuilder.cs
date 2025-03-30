using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing.Reader;
using System.Security.Cryptography.Xml;
using System.Text;

namespace BluetoothLE
{
    public enum Target { FTP, MAP, AC, NM };

    public record struct WorkoutItem(Target Target, int Value);

    public class WorkoutItemTransformer : IItemTransformer<WorkoutItem>
    {
        public string Colour(WorkoutItem item) => item.Target switch
        {
            Target.FTP => "rgb(11 190 235)",
            Target.MAP => "rgb(255 197 0)",
            Target.AC => "rgb(251 139 4)",
            Target.NM => "rgb(237 31 143)",
            _ => throw new ArgumentException("Invalid target type")
        };

        public int Value(WorkoutItem item) => item.Value;
    }

    public class ValueTransformer(string FixedColour) : IItemTransformer<int>
    {
        public string Colour(int item) => FixedColour;
        public int Value(int item) => item;
    }

    public interface IItemTransformer<T>
    {
        public string Colour(T item);
        public int Value(T item);
    }

    public class GraphBuilder<T>(IItemTransformer<T> Transformer, int Width, int Height, int MaxValue)
    {
        public record Segment(string Fill, string Stroke, string Colour, string? Class = null);

        public int Width = Width;
        public int Height = Height;

        private StringBuilder fillBuilder = new(4096);
        private StringBuilder strokeBuilder = new(4096);

        public int? IndicatorPosition { get; set; } = null;
        public bool JoinBlocks { get; set; } = true;
        public bool CreateStroke { get; set; } = true;
        public bool CreateFill { get; set; } = true;

        public T[] Data = new T[Width];

        public int Length { get; set; }

        public double ScaleFactor { get; set; } = 4.0;

        public IEnumerable<Segment> Paths(bool debug=false) => Paths(.., 0, debug);

        public IEnumerable<Segment> Paths(Range range, bool debug = false) => Paths(range, 0, debug);

        public IEnumerable<Segment> Paths(Range range, int offset, bool debug=false)
        {
            string Colour(int index) => Transformer.Colour(Data[index]);
            double ScaledValue(int index) => GetScaledValue(Transformer.Value(Data[index]));

            var (begin, length) = range.GetOffsetAndLength(Length);
            
            if (length > Width)
            {
                throw new ArgumentOutOfRangeException($"Data exceeds graph size");
            }

            if (length == 0)
            {
                return [];
            }

            double currentValue = ScaledValue(0);
            string currentColour = Colour(0);

            bool isStartOfBlock = true;

            List<Segment> paths = [];
            fillBuilder.Clear();
            strokeBuilder.Clear();

            //for (int ix = 0; ix < Length; ++ix)
            for (int ix = begin; ix < length; ++ix)
            {
                double previousValue = currentValue;
                currentValue = ScaledValue(ix);

                string previousColour = currentColour;
                currentColour = Colour(ix);

                double startX = (double)ix / ScaleFactor;
                double endX = (double)(ix + 1) / ScaleFactor;

                // isStartOfBlock is true when ix == 0, or lastColour != currentColour
                if (isStartOfBlock)
                {
                    if (ix > 0)
                    {
                        if (CreateFill)
                        {
                            fillBuilder.Append($"L{startX},{Height}");

                            paths.Add(new(fillBuilder.ToString(), strokeBuilder.ToString(), previousColour));

                            fillBuilder.Clear();
                        }

                        if (CreateStroke)
                        {
                            strokeBuilder.Clear();
                        }
                    }

                    if (CreateFill)
                    {
                        fillBuilder.Append($"M{startX},{Height}");
                        if (JoinBlocks)
                            fillBuilder.Append($"L{startX},{previousValue}");
                        else
                            fillBuilder.Append($"L{startX},{currentValue}");
                        fillBuilder.Append($"L{endX},{currentValue}");
                    }

                    if (CreateStroke)
                    {
                        if (JoinBlocks)
                        {
                            strokeBuilder.Append($"M{startX},{previousValue}");
                        }
                        else
                        {
                            strokeBuilder.Append($"M{startX},{currentValue}");
                        }

                        strokeBuilder.Append($"L{endX},{currentValue}");
                    }
                }
                else
                {
                    if (CreateFill)
                        fillBuilder.Append($"L{endX},{currentValue}");

                    if (CreateStroke)
                    {
                        strokeBuilder.Append($"L{endX},{currentValue}");
                    }
                }

                if (ix < Length - 1)
                {
                    isStartOfBlock = Transformer.Colour(Data[ix]) != Transformer.Colour(Data[ix + 1]);
                }
            }

            if (!isStartOfBlock)
            {
                if (CreateFill)
                {
                    fillBuilder.Append($"L{(double)(Length) / ScaleFactor},{Height}");
                }

                paths.Add(new(fillBuilder.ToString(), strokeBuilder.ToString(), currentColour));
            }

            if (IndicatorPosition is int position)
            {
                double x0 = position / (1000 * ScaleFactor);
                double x1 = x0 + 1;
                double y0 = Height;
                double y1 = 0;

                string verticalLine = $"M{x0},{y0} L{x0},{y1} L{x1},{y1} L{x1},{y0} L{x0},{y0}";
                string bottomTriangle = $"M{x0 - 4},{y0} L{x0},{y0 - 4} L{x1},{y0 - 4} L{x1 + 4},{y0} L{x0 - 4},{y0}";
                string topTriangle = $"M{x0 - 4},{y1} L{x0},{y1 + 4} L{x1},{y1 + 4} L{x1 + 4},{y1} L{x0 - 4},{y1}";
                paths.Add(new(verticalLine, verticalLine, "yellow", "indicator"));
                paths.Add(new(bottomTriangle, bottomTriangle, "yellow", "indicator"));
                paths.Add(new(topTriangle, topTriangle, "yellow", "indicator"));
            }

            return paths;
        }

        public IEnumerable<Segment> PathsScaled(double width)
        {
            string Colour(int index) => Transformer.Colour(Data[index]);
            double ScaledValue(int index) => GetScaledValue(Transformer.Value(Data[index]));

            double multiplier = width / (double)Length;

            if (Length > Width)
            {
                throw new ArgumentOutOfRangeException($"Data exceeds graph size");
            }

            if (Length == 0)
            {
                return [];
            }

            double currentValue = ScaledValue(0);
            string currentColour = Colour(0);

            bool isStartOfBlock = true;

            List<Segment> paths = [];
            fillBuilder.Clear();
            strokeBuilder.Clear();

            for (int ix = 0; ix < Length; ++ix)
            {
                double previousValue = currentValue;
                currentValue = ScaledValue(ix);

                string previousColour = currentColour;
                currentColour = Colour(ix);

                double startX = ((double)ix / 4.0) * multiplier;
                double endX = ((double)(ix + 1) / 4.0) * multiplier;

                // isStartOfBlock is true when ix == 0, or lastColour != currentColour
                if (isStartOfBlock)
                {
                    if (ix > 0)
                    {
                        if (CreateFill)
                        {
                            fillBuilder.Append($"L{startX},{Height}");

                            paths.Add(new(fillBuilder.ToString(), strokeBuilder.ToString(), previousColour));

                            fillBuilder.Clear();
                        }

                        if (CreateStroke)
                        {
                            strokeBuilder.Clear();
                        }
                    }

                    if (CreateFill)
                    {
                        fillBuilder.Append($"M{startX},{Height}");
                        if (JoinBlocks)
                            fillBuilder.Append($"L{startX},{previousValue}");
                        else
                            fillBuilder.Append($"L{startX},{currentValue}");
                        fillBuilder.Append($"L{endX},{currentValue}");
                    }

                    if (CreateStroke)
                    {
                        if (JoinBlocks)
                        {
                            strokeBuilder.Append($"M{startX},{previousValue}");
                        }
                        else
                        {
                            strokeBuilder.Append($"M{startX},{currentValue}");
                        }

                        strokeBuilder.Append($"L{endX},{currentValue}");
                    }
                }
                else
                {
                    if (CreateFill)
                        fillBuilder.Append($"L{endX},{currentValue}");

                    if (CreateStroke)
                    {
                        strokeBuilder.Append($"L{endX},{currentValue}");
                    }
                }

                if (ix < Length - 1)
                {
                    isStartOfBlock = Transformer.Colour(Data[ix]) != Transformer.Colour(Data[ix + 1]);
                }
            }

            if (!isStartOfBlock)
            {
                if (CreateFill)
                {
                    fillBuilder.Append($"L{((double)(Length) / 4.0) * multiplier},{Height}");
                }

                paths.Add(new(fillBuilder.ToString(), strokeBuilder.ToString(), currentColour));
            }

            return paths;
        }

        double GetScaledValue(int value)
        {
            double clamped = Math.Min(value, MaxValue);
            double scaled = (clamped / (double)MaxValue) * Height;

            return Height - scaled;
        }
    }
}
