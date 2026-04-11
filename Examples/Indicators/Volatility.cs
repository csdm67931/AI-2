using Runtime.Script;
using Runtime.Script.Charts;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradeApi;
using TradeApi.History;
using TradeApi.Indicators;
using TradeApi.ToolBelt;

namespace Volatility
{
    #region (ATR)Average True Range
    public class ATR : IndicatorBuilder
    {
        public ATR()
            : base()
        {
            #region Initialization
            Credentials.ProjectName = "Average True Range";
            #endregion

            Lines.Set("ATR");

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "Period of Moving Average", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 13;

        [InputParameter(InputType.Combobox, "Type of Moving Average", 1)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode MAType = MAMode.SMA;

        BuiltInIndicator ma;

        public override void Init()
        {
            ScriptShortName = $"ATR({MAPeriod})";
            ma = IndicatorsManager.BuildIn.MA(GetTrueRange, MAPeriod, MAType);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < MAPeriod)
                return;

            Lines["ATR"].SetValue(ma.GetValue());
        }

        private double GetTrueRange(int index)
        {
            double hi = HistoryDataSeries.GetValue(PriceType.High, index);
            double lo = HistoryDataSeries.GetValue(PriceType.Low, index);
            double prevClose = (HistoryDataSeries.Count > index + 1) ? HistoryDataSeries.GetValue(PriceType.Close, index + 1)
                : HistoryDataSeries.GetValue(PriceType.Close, index);

            return Math.Max(hi - lo, Math.Max(Math.Abs(prevClose - hi), Math.Abs(prevClose - lo)));
        }
    }
    #endregion

    #region (SD)Standard Deviation
    public class SD : IndicatorBuilder
    {
        public SD()
            : base()
        {
            Credentials.ProjectName = "Standard Deviation";

            Lines.Set("SD");

            SeparateWindow = true;
        }

        [InputParameter(InputType.Combobox, "Sources prices for MA", 0)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrice = PriceType.Close;

        [InputParameter(InputType.Combobox, "Type of Moving Average", 1)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode MAType = MAMode.SMA;

        [InputParameter(InputType.Numeric, "Period of indicator", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 20;

        BuiltInIndicator ma;

        public override void Init()
        {
            ScriptShortName = $"SD({MAPeriod})";

            ma = IndicatorsManager.BuildIn.MA(HistoryDataSeries, MAPeriod, MAType);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < MAPeriod)
                return;

            double dAmount = 0;
            double movingAverage = ma.GetValue();
            for (int j = 0; j < MAPeriod; j++)
                dAmount += Math.Pow(HistoryDataSeries.GetValue(SourcePrice, j) - movingAverage, 2);

            Lines["SD"].SetValue(Math.Sqrt(dAmount / MAPeriod));
        }
    }
    #endregion

    #region (HV)Historical Volatility 
    public class HV : IndicatorBuilder
    {
        public HV() : base()
        {
            Credentials.ProjectName = "Historical Volatility";

            Lines.Set("HV");
            Lines["HV"].Color = Color.Red;
            Lines["HV"].Width = 2;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "STDPeriod", 0)]
        [SimpleNumeric(2D, 99999D)]
        public int STDPeriod = 30;

        [InputParameter(InputType.Numeric, "VolatilityPeriod", 1)]
        [SimpleNumeric(1D, 99999D)]
        public int VolatilityPeriod = 365;

        [InputParameter(InputType.Combobox, "Sources prices for MA", 2)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrice = PriceType.Close;

        [InputParameter(InputType.Checkbox, "All history", 3)]
        public bool isAllHistoryMode = true;

        [InputParameter(InputType.Numeric, "History period", 4)]
        [SimpleNumeric(0D, 99999D)]
        public int PercentilePeriod = 100;

        [InputParameter(InputType.Combobox, "Sources prices for HV", 5)]
        [ComboboxItem("HV and percentile", DisplayMode.HVAndPercentile)]
        [ComboboxItem("Only percentile", DisplayMode.PercentileOnly)]
        public DisplayMode DisplayMode = DisplayMode.HVAndPercentile;

        Color indicatorColor;
        Pen pen;
        Font font;
        Brush brush;

        public List<double> _hvCollection;

        public override void Init()
        {
            ScriptShortName = string.Format("HV ({0}; {1})", STDPeriod, DisplayMode.ToString());
            _hvCollection = new List<double>();

            indicatorColor = Lines["HV"].Color;

            pen = new Pen(indicatorColor);
            font = new Font("Arial", 10);
            brush = new SolidBrush(indicatorColor);

            var chart = ChartSource.GetChartControl() as System.Windows.Forms.Control;
            chart.MouseMove += ProcessMouseMove;
        }

        public override void Complete()
        {
            if (ChartSource != null)
            {
                var chart = ChartSource.GetChartControl() as System.Windows.Forms.Control;
                chart.MouseMove -= ProcessMouseMove;
            }
        }

        public override void Update(TickStatus args)
        {
            if (args != TickStatus.IsQuote)
            {
                if (HistoryDataSeries.Count < STDPeriod)
                    return;

                double hV = getStandardDeviation(STDPeriod) * Math.Sqrt(VolatilityPeriod) * 100;
                _hvCollection.Insert(0, hV);

                if (IsHistoryLoaded() && DisplayMode == DisplayMode.PercentileOnly)
                {
                    for (int i = 0; i < _hvCollection.Count; i++)
                    {
                        double percentile = GetPercentile(i, _hvCollection[i]);
                        Lines["HV"].SetValue(percentile, i);
                    }
                }
                return;
            }

            if (HistoryDataSeries.Count < STDPeriod)
                return;

            var hv = getStandardDeviation(STDPeriod) * Math.Sqrt(VolatilityPeriod) * 100;
            _hvCollection[0] = hv;

            if (DisplayMode == DisplayMode.HVAndPercentile)
                Lines["HV"].SetValue(hv);
            else if (DisplayMode == DisplayMode.PercentileOnly)
                Lines["HV"].SetValue(GetPercentile(0, hv));

        }

        double getStandardDeviation(int period)
        {
            var data = Enumerable.Range(0, period - 1).Select(i => Math.Log(HistoryDataSeries.GetValue(SourcePrice, i) / HistoryDataSeries.GetValue(SourcePrice, i + 1)));

            double sumOfDerivationAverage = data.Sum(value => value * value) / (period - 1);
            double average = Enumerable.Average(data);
            return Math.Sqrt(sumOfDerivationAverage - average * average);
        }

        private double CalcPecentileInRangeOf(int startIndex, int endIndex, double value)
        {
            double count = 0.0;
            for (int i = startIndex; i > endIndex; i--)
            {
                if (_hvCollection[i] <= value)
                    count += 1.0;
            }
            return count / (startIndex - endIndex) * 100.0;
        }

        #region Misc
        public override void OnPaintChart(object sender, PaintChartEventArgs args)
        {
            if (DisplayMode == DisplayMode.HVAndPercentile)
            {
                //finding the current window
                int winNumber = ChartSource.FindWindow(this);
                var windows = ChartSource.GetAllWindows();
                if (winNumber < 0 || winNumber >= windows.Count)
                    return;

                Point clientPoint = ChartSource.PointToChart(System.Windows.Forms.Cursor.Position);

                var timePrice = ChartSource.GetTimeValue(clientPoint, winNumber);
                if (timePrice.TimeUtc == DateTime.MinValue)
                    return;

                int offset = HistoryDataSeries.FindInterval(timePrice.TimeUtc);
                if (offset == -1 || offset < STDPeriod)
                    return;


                Rectangle rect = windows[winNumber].WindowRectangle;

                var targetIndex = HistoryDataSeries.Count - offset - 1;
                double hv = _hvCollection[targetIndex];

                args.Graphics.DrawLine(pen, clientPoint.X, rect.Y, clientPoint.X, rect.Y + rect.Height);
                args.Graphics.DrawString(string.Format("Percentile: {0:F}%", GetPercentile(targetIndex, hv)), this.font, brush, rect.X + 5, rect.Y + 5);
            }

        }

        void ProcessMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            ChartSource.GUIRefresh();
        }

        private double GetPercentile(int targetIndex, double value)
        {
            double percentile;

            if (isAllHistoryMode)
                percentile = CalcPecentileInRangeOf(_hvCollection.Count - 1, 0, value);
            else
            {
                var startIndex = targetIndex + PercentilePeriod > _hvCollection.Count - 1 ? _hvCollection.Count - 1 : targetIndex + PercentilePeriod;
                percentile = CalcPecentileInRangeOf(startIndex, targetIndex, value);
            }

            return percentile;
        }

        private bool IsHistoryLoaded()
        {
            int index = HistoryDataSeries.FindInterval(DateTime.UtcNow);
            return index == 0;
        }
        #endregion
    }
    public enum DisplayMode
    {
        HVAndPercentile,
        PercentileOnly
    }

    #endregion
}