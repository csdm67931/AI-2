using Runtime.Script;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeApi;
using TradeApi.History;
using TradeApi.Indicators;
using TradeApi.ToolBelt;

namespace Channels
{
    #region (BB)Bollinger Band
    public class BB : IndicatorBuilder
    {
        public BB()
            : base()
        {
            Credentials.ProjectName = "Bollinger Bands";
            ScriptShortName = "BB Test";

            Lines.Set("LOWER_BAND");
            Lines["LOWER_BAND"].Color = Color.Green;

            Lines.Set("UPPER_BAND");
            Lines["UPPER_BAND"].Color = Color.Red;

            Lines.Set("MIDDLE_BAND");
            Lines["MIDDLE_BAND"].Color = Color.Gray;

            SeparateWindow = false;
        }

        [InputParameter(InputType.Combobox, "Sources prices for MA", 0)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrices = PriceType.Low;

        [InputParameter(InputType.Combobox, "Type of Moving Average", 1)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode MAType = MAMode.SMA;

        [InputParameter(InputType.Numeric, "Period of MA for envelopes", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 5;

        [InputParameter(InputType.Numeric, "Value of confidence interval", 3)]
        [SimpleNumeric(1)]
        public double D = 1D;

        private BuiltInIndicator ma;

        public override void Init()
        {
            ScriptShortName = string.Format("BB({0};{1})", MAPeriod, D);
            ma = IndicatorsManager.BuildIn.MA(HistoryDataSeries, MAPeriod, MAType, 0, SourcePrices);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count <= MAPeriod)
                return;

            double maValue = ma.Lines[0].GetValue();

            double summa = 0.0;
            for (int i = 0; i < MAPeriod; i++)
                summa += Math.Pow(HistoryDataSeries.GetValue(SourcePrices, i) - maValue, 2);

            summa = D * Math.Sqrt(summa / MAPeriod);

            Lines["MIDDLE_BAND"].SetValue(maValue);
            Lines["LOWER_BAND"].SetValue(maValue - summa);
            Lines["UPPER_BAND"].SetValue(maValue + summa);
        }

        public override void Complete()
        { }
    }
    #endregion

    #region (BBF)Bollinger Band Flat
    public class BBF : IndicatorBuilder
    {
        public BBF()
            : base()
        {
            Credentials.ProjectName = "Bollinger Bands Flat";

            Lines.Set("line1");
            Lines["line1"].Color = Color.Aqua;

            Lines.Set("line2");
            Lines["line2"].Color = Color.Red;

            Lines.Set("line3");
            Lines["line3"].Color = Color.Red;

            Lines.Set("line4");
            Lines["line4"].Color = Color.FromArgb(0, 51, 252);

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "Period", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int BBPeriod = 9;

        [InputParameter(InputType.Numeric, "Shift", 1)]
        [SimpleNumeric(0)]
        public int Shift = 0;

        [InputParameter(InputType.Combobox, "Type of Moving Average", 2)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode MAType = MAMode.SMA;

        [InputParameter(InputType.Combobox, "Sources prices for MA", 3)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrices = PriceType.Close;

        [InputParameter(InputType.Numeric, "Deviation", 4)]
        [SimpleNumeric(1)]
        public double Deviation = 1.5;

        private BuiltInIndicator ma;
        private BuiltInIndicator stdDev;

        public override void Init()
        {
            ScriptShortName = string.Format("BBF({0};{1})", BBPeriod, Deviation);
            for (int i = 0; i < 4; i++)
                Lines[i].SetLineBegin(BBPeriod);

            ma = IndicatorsManager.BuildIn.MA(HistoryDataSeries, BBPeriod, MAType, 0, SourcePrices);
            stdDev = IndicatorsManager.BuildIn.StdDev(HistoryDataSeries, BBPeriod, MAType, Shift, SourcePrices);
        }

        public override void Update(TickStatus args)
        {
            double std = Deviation * stdDev.Lines[0].GetValue();

            Lines["line1"].SetValue(0);
            Lines["line2"].SetValue(std);
            Lines["line3"].SetValue(-std);
            Lines["line4"].SetValue(HistoryDataSeries.GetValue(PriceType.Close) - ma.Lines[0].GetValue());
        }
    }
    #endregion

    #region (CHANNEL)Price Channel
    public class CHANNEL : IndicatorBuilder
    {
        public CHANNEL()
            : base()
        {
            Credentials.ProjectName = "Price Channel";

            Lines.Set("line1");
            Lines["line1"].Color = Color.Red;

            Lines.Set("line2");
            Lines["line2"].Color = Color.Blue;

            Lines.Set("line3");
            Lines["line3"].Color = Color.FromArgb(0, 163, 44);
            Lines["line3"].Style = LineStyle.ShapedLine;

            SeparateWindow = false;
        }

        [InputParameter(InputType.Numeric, "Period of price channel", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int PrPeriod = 10;

        private const double MIN = 10000000.0;

        public override void Init()
        {
            ScriptShortName = string.Format("CHANNEL({0})", PrPeriod);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < PrPeriod)
                return;

            double high = 0.0;
            double low = MIN;

            for (int i = 0; i < PrPeriod; i++)
            {
                double price = HistoryDataSeries.GetValue(PriceType.High, i);
                if (price > high)
                    high = price;
                price = HistoryDataSeries.GetValue(PriceType.Low, i);
                if (price < low)
                    low = price;
            }

            if (low < MIN)
            {
                Lines["line1"].SetValue(high);
                Lines["line2"].SetValue(low);
                Lines["line3"].SetValue((high + low) / 2);
            }
        }
    }
    #endregion

    #region (KELTNER)Keltner Channel
    public class KELTNER : IndicatorBuilder
    {
        public KELTNER()
            : base()
        {
            Credentials.ProjectName = "Keltner Channel";

            Lines.Set("line1");
            Lines["line1"].Color = Color.Coral;

            Lines.Set("line2");
            Lines["line2"].Color = Color.Red;

            Lines.Set("line3");
            Lines["line3"].Color = Color.Purple;


            SeparateWindow = false;
        }

        [InputParameter(InputType.Combobox, "Sources prices for MA", 0)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrices = PriceType.Close;

        [InputParameter(InputType.Combobox, "Type of Moving Average", 1)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode MAType = MAMode.SMA;

        [InputParameter(InputType.Numeric, "Period of MA for Keltner's Channel", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 5;

        [InputParameter(InputType.Numeric, "Coefficient of channel's width", 3)]
        [SimpleNumeric(1D, 20)]
        public double CoefChannelWidth = 2;

        [InputParameter(InputType.Combobox, "Bands style", 4)]
        [ComboboxItem("TrueRange", BandStyle.TrueRange)]
        [ComboboxItem("TradingRange", BandStyle.TradingRange)]

        public BandStyle bandStyle = BandStyle.TrueRange;

        private BuiltInIndicator ma;

        private BuiltInIndicator maaTR;

        public override void Init()
        {
            ScriptShortName = string.Format("KELTNER({0};{1};{2})", MAPeriod, CoefChannelWidth, SourcePrices);
            ma = IndicatorsManager.BuildIn.MA(HistoryDataSeries, MAPeriod, MAType, 0, SourcePrices);
            maaTR = bandStyle == BandStyle.TrueRange ? IndicatorsManager.BuildIn.MA(GetTrueRange, MAPeriod, MAType) : IndicatorsManager.BuildIn.MA(GetTradingRange, MAPeriod, MAType);
        }

        public override void Update(TickStatus args)
        {
            double middleTR = ma.Lines[0].GetValue();
            double diffMA = maaTR.Lines[0].GetValue();

            Lines[0].SetValue(middleTR + diffMA);
            Lines[1].SetValue(middleTR);
            Lines[2].SetValue(middleTR - diffMA);
        }

        private double GetTrueRange(int index = 0)
        {
            double hi = HistoryDataSeries.GetValue(PriceType.High, index);
            double lo = HistoryDataSeries.GetValue(PriceType.Low, index); ;
            double prevClose = (HistoryDataSeries.Count > index + 1) ? HistoryDataSeries.GetValue(PriceType.Close, index + 1) : HistoryDataSeries.GetValue(PriceType.Close, index);

            return Math.Max(hi - lo, Math.Max(Math.Abs(hi - prevClose), Math.Abs(lo - prevClose))) * CoefChannelWidth;
        }

        private double GetTradingRange(int index = 0)
        {
            double hi = HistoryDataSeries.GetValue(PriceType.High, index);
            double lo = HistoryDataSeries.GetValue(PriceType.Low, index);

            return (hi - lo) * CoefChannelWidth;
        }

        public enum BandStyle
        {
            TrueRange = 0,
            TradingRange = 1
        }
    }
    #endregion

    #region (MAE)Moving Average Envelope
    public class MAE : IndicatorBuilder
    {
        public MAE()
            : base()
        {
            Credentials.ProjectName = "Moving Average Envelope";

            Lines.Set("LOWER_BAND");
            Lines["LOWER_BAND"].Color = Color.Purple;

            Lines.Set("UPPER_BAND");
            Lines["UPPER_BAND"].Color = Color.LightSeaGreen;

            SeparateWindow = false;
        }

        [InputParameter(InputType.Combobox, "Sources prices for MA", 0)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrices = PriceType.Low;

        [InputParameter(InputType.Combobox, "Type of Moving Average", 1)]
        [ComboboxItem("Simple", MAMode.SMA)]
        [ComboboxItem("Exponential", MAMode.EMA)]
        [ComboboxItem("Modified", MAMode.SMMA)]
        [ComboboxItem("Linear Weighted", MAMode.LWMA)]
        public MAMode MAType = MAMode.SMA;

        [InputParameter(InputType.Numeric, "Period of MA for envelopes", 2)]
        [SimpleNumeric(1D, 99999D)]
        public int MAPeriod = 5;

        [InputParameter(InputType.Numeric, "Upband deviation in %", 3)]
        [SimpleNumeric(1)]
        public double UpShift = 0.1;

        [InputParameter(InputType.Numeric, "Downband deviation in % ", 4)]
        [SimpleNumeric(1)]
        public double DownShift = 0.1;

        private BuiltInIndicator ma;

        public override void Init()
        {
            ScriptShortName = string.Format("MAE({0};{1};{2})", MAPeriod, UpShift, DownShift);
            ma = IndicatorsManager.BuildIn.MA(HistoryDataSeries, MAPeriod, MAType, 0, SourcePrices);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < MAPeriod)
                return;

            double maValue = ma.Lines[0].GetValue();
            Lines["LOWER_BAND"].SetValue((1D - DownShift * 0.01) * maValue);
            Lines["UPPER_BAND"].SetValue((1D + UpShift * 0.01) * maValue);
        }
    }
    #endregion
}
