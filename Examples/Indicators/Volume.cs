using Runtime.Script;
using System;
using System.Drawing;
using TradeApi;
using TradeApi.History;
using TradeApi.Indicators;
using TradeApi.ToolBelt;

namespace Volume
{
    #region (MFI) MFI
    public class MFI : IndicatorBuilder
    {
        public MFI()
            : base()
        {
            #region Initialization
            Credentials.ProjectName = "Money Flow Index";
            #endregion

            Lines.Set("MFI");

            Thresholds.Set("Up");
            Thresholds["Up"].Color = Color.Gray;
            Thresholds["Up"].Level = 80;

            Thresholds.Set("Down");
            Thresholds["Down"].Color = Color.Gray;
            Thresholds["Down"].Level = 20;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "MFI Period", 0)]
        [SimpleNumeric(1.0, 99999.0)]
        public int mfiPeriod = 14;

        BuiltInIndicator mfi;

        public override void Init()
        {
            ScriptShortName = $"MFI({mfiPeriod})";

            mfi = IndicatorsManager.BuildIn.MFI(HistoryDataSeries, mfiPeriod);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < mfiPeriod + 1)
                return;

            Lines["MFI"].SetValue(mfi.GetValue());
        }
    }
    #endregion

    #region (PVI) Positive Volume Index
    public class PVI : IndicatorBuilder
    {
        public PVI()
            : base()
        {
            Credentials.ProjectName = "Positive Volume Index";

            Lines.Set("PVI");

            SeparateWindow = true;
        }

        [InputParameter(InputType.Combobox, "Sources prices for MA", 1)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrice = PriceType.Close;

        public override void Init()
        {
            ScriptShortName = "PVI";
            Lines[0].SetLineBegin(1);
        }

        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count == 1)
            {
                Lines["PVI"][0] = 1;
                return;
            }

            double curVolume = 0;
            double prevVolume = 0;

            BarData barData = HistoryDataSeries as BarData;
            if (barData != null)
            {
                var vol0 = barData.GetVolume();
                var vol1 = barData.GetVolume(1);
                curVolume = !double.IsNaN(vol0) ? vol0 : barData.GetTicks();
                prevVolume = !double.IsNaN(vol1) ? vol1 : barData.GetTicks(1);
            }
            else if (HistoryDataSeries is BBOData)
            {
                BBOData tickData = (BBOData)HistoryDataSeries;
                curVolume = tickData.GetVolume();
                prevVolume = tickData.GetVolume(1);
            }
            else
            {
                TradeData tradeData = (TradeData)HistoryDataSeries;
                curVolume = tradeData.GetSize();
                prevVolume = tradeData.GetSize(1);
            }

            double prevPVI = Lines["PVI"].GetValue(1);
            if (curVolume > prevVolume)
            {
                double curPrice = HistoryDataSeries.GetValue(SourcePrice);
                double prevPrice = HistoryDataSeries.GetValue(SourcePrice, 1);

                double curPVI = prevPVI + (prevPVI * (curPrice - prevPrice) / prevPrice);
                Lines["PVI"].SetValue(curPVI);
            }
            else
            {
                Lines["PVI"].SetValue(prevPVI);
            }
        }
    }
    #endregion

    #region (VOLUME) Volume
    public class VOLUME : IndicatorBuilder
    {
        public VOLUME() : base()
        {
            Credentials.ProjectName = "Volume";

            Lines.Set("Volume");
            Lines["Volume"].Color = Color.Green;
            Lines["Volume"].Width = 2;
            Lines["Volume"].Style = LineStyle.Histogram;

            SeparateWindow = true;
        }

        public override void Init()
        {
            ScriptShortName = "VOL";
        }

        public override void Update(TickStatus args)
        {
            int count = HistoryDataSeries.Count;

            double currVol, prevVol;

            if (HistoryDataSeries is BarData barData)
            {
                var vol0 = barData.GetVolume();
                var vol1 = count > 1 ?  barData.GetVolume(1) : 0;
                currVol = !double.IsNaN(vol0) ? vol0 : barData.GetTicks();
                prevVol = !double.IsNaN(vol1) ? vol1 : barData.GetTicks(1);
            }
            else if (HistoryDataSeries is BBOData bboData)
            {
                currVol = bboData.GetVolume();
                prevVol = count > 1 ? bboData.GetVolume(1) : 0;
            }
            else
            {
                TradeData tradeData = (TradeData)HistoryDataSeries;
                currVol = tradeData.GetSize();
                prevVol = count > 1 ? tradeData.GetSize(1) : 0;
            }

            Lines["Volume"].SetValue(currVol);

            if (prevVol < currVol)
            {
                Lines["Volume"].Markers.Set(Color.Green);
            }
            else if (prevVol > currVol)
            {
                Lines["Volume"].Markers.Set(Color.Red);
            }
            else
            {
                Lines["Volume"].Markers.Set(Color.Gray);
            }
        }
    }
    #endregion

    #region (VWAP) Volume Weighted Average Price
    public class VWAP : IndicatorBuilder
    {
        private enum VWAPHistoryStatus { Invalid = -1, None = 0, Loading = 1, Processed = 2 }
        public class VWAPCalculator
        {
            public int DaysCount { get; private set; }
            public TimeInterval TimeInterval { get; private set; }

            public double SumSize { get; private set; } = 0D;
            public double SumPriceSize { get; private set; } = 0D;
            public double LastVWAPValue { get; private set; } = 0D;
            public Interval CurrentCycleInterval { get; private set; }

            public VWAPCalculator(int daysCount, TimeInterval timeInterval)
            {
                this.DaysCount = daysCount;
                this.TimeInterval = timeInterval;
            }

            public void CalculateVWAP(DateTime dateTime, double volume, double price)
            {
                if (this.TryCreateNewCycle(dateTime))
                {
                    this.SumSize = 0d;
                    this.SumPriceSize = 0d;
                    this.LastVWAPValue = 0d;
                }
                if (this.AllowCalculateVWAP(dateTime))
                {
                    this.SumSize += volume;
                    this.SumPriceSize += price * volume;
                    this.LastVWAPValue = SumPriceSize / SumSize;
                }
            }
            public bool IsValidTimeFrame(HistoricalRequest historicalRequest)
            {
                if (historicalRequest is TimeHistoricalRequest time && time.Period != Period.Second)
                    return true;
                else if (historicalRequest is KagiHistoricalRequest kagi && kagi.BuildFrom != BuildFrom.Ticks && kagi.BuildFrom != BuildFrom.Second)
                    return true;
                else if (historicalRequest is PointAndFigureHistoricalRequest pointAndFigure && pointAndFigure.BuildFrom != BuildFrom.Ticks && pointAndFigure.BuildFrom != BuildFrom.Second)
                    return true;
                else if (historicalRequest is HeikinAshiHistoricalRequest heikinAshi && heikinAshi.Period != Period.Second)
                    return true;
                else
                    return false;
            }
            public bool AllowSetVWAP(DateTime dateTime) => this.CurrentCycleInterval != null && this.CurrentCycleInterval.FromUtc <= dateTime && this.CurrentCycleInterval.ToUtc >= dateTime;

            private bool AllowCalculateVWAP(DateTime dateTime)
            {
                if (this.AllowSetVWAP(dateTime))
                {
                    bool isNextDayIntervalTime = this.TimeInterval.From > this.TimeInterval.To;
                    if (isNextDayIntervalTime)
                        return dateTime.TimeOfDay >= this.TimeInterval.From || dateTime.TimeOfDay < this.TimeInterval.To;
                    else
                        return dateTime.TimeOfDay >= this.TimeInterval.From && dateTime.TimeOfDay < this.TimeInterval.To;
                }
                else
                    return false;
            }
            private bool TryCreateNewCycle(DateTime dateTime)
            {
                if (this.CurrentCycleInterval == null || CurrentCycleInterval.ToUtc <= dateTime)
                {
                    this.CurrentCycleInterval = CreateCycleInterval(dateTime);
                    return true;
                }
                else
                    return false;
            }
            private Interval CreateCycleInterval(DateTime dateTime)
            {
                DateTime date = dateTime.Date;
                if (CurrentCycleInterval != null && CurrentCycleInterval.ToUtc.Date == date)
                    date = date.AddDays(1);

                bool isNextDayIntervalTime = this.TimeInterval.From > this.TimeInterval.To;
                var from = date.Add(this.TimeInterval.From);
                var to = date.AddDays(this.DaysCount - 1).Add(isNextDayIntervalTime ? this.TimeInterval.To.Add(TimeSpan.FromDays(1)) : this.TimeInterval.To);
                to = to.AddTicks(-1);
                return new Interval(from, to);
            }
        }

        private VWAPHistoryStatus vwapHistoryStatus = VWAPHistoryStatus.None;

        private HistoricalRequest vwapHistoricalRequest;

        [InputParameter(InputType.Numeric, "Days number in cycle", 0)]
        [SimpleNumeric(1.0, 366.0, 0, 1)]
        public int DaysCount = 1;

        [InputParameter(InputType.TimeInterval, "Start/End day time", 1)]
        public TimeInterval TimeInterval = new TimeInterval();

        private VWAPCalculator calculator;
        private bool byTrades = false;

        public VWAP() : base()
        {
            Credentials.ProjectName = "Volume Weighted Average Price";

            Lines.Set("VWAP");
            Lines["VWAP"].Color = Color.DodgerBlue;
            Lines["VWAP"].Width = 2;

            base.SeparateWindow = false;
        }

        public override void Init()
        {
            this.calculator = new VWAPCalculator(DaysCount, TimeInterval);

            ScriptShortName = $"VWAP({DaysCount})";

            var currentHistoricalRequest = this.HistoryDataSeries.HistoricalRequest;
            if (this.calculator.IsValidTimeFrame(currentHistoricalRequest))
            {
                this.HistoricalDataManager.OnLoaded += this.OnLoaded;
                this.vwapHistoricalRequest = new TimeHistoricalRequest(currentHistoricalRequest.Instrument, currentHistoricalRequest.DataType, Period.Minute, 1);
            }
            else
                this.vwapHistoryStatus = VWAPHistoryStatus.Invalid;

            this.byTrades = currentHistoricalRequest.DataType == DataType.Trade;
        }

        private void OnLoaded(HistoricalData historicalData) => this.ProcessVWAPHistory(historicalData);

        public override void Update(TickStatus args)
        {
            switch (vwapHistoryStatus)
            {
                case VWAPHistoryStatus.Invalid:
                    break;
                case VWAPHistoryStatus.None:
                    this.LoadVWAPHistory();
                    break;
                case VWAPHistoryStatus.Processed:
                    if (args == TickStatus.IsQuote)
                    {
                        var dateTime = Tools.TimeConvertor.UtcToCurrentTimeZone(DateTime.UtcNow);
                        var volume = this.GetLastSize();
                        var price = this.GetLastPrice();
                        this.UpdateVWAPLine(dateTime, volume, price, 0);
                    }
                    break;
            }
        }

        private void LoadVWAPHistory()
        {
            if (this.HistoryDataSeries != null && this.HistoryDataSeries.Count > 0)
            {
                vwapHistoryStatus = VWAPHistoryStatus.Loading;

                var from = this.HistoryDataSeries.GetTimeUtc(this.HistoryDataSeries.Count - 1);
                var to = Tools.TimeConvertor.СurrentTimeZoneToUtc(DateTime.Now.Date.AddDays(1));
                var interval = new Interval(from, to);
                this.HistoricalDataManager.Get(vwapHistoricalRequest, interval);
            }
        }
        private void ProcessVWAPHistory(HistoricalData historicalData)
        {
            if (historicalData is BarData barData)
            {
                for (int i = barData.Count - 1; i >= 0; i--)
                {
                    var volume = this.byTrades ? barData.GetVolume(i) : barData.GetTicks(i);
                    var price = barData.GetValue(PriceType.Typical, i);
                    var intervalDateTime = historicalData.GetTimeUtc(i);
                    var offset = this.HistoryDataSeries.FindInterval(intervalDateTime);
                    if (offset == -1)
                        continue;
                    var barTime = Tools.TimeConvertor.UtcToCurrentTimeZone(intervalDateTime);
                    this.UpdateVWAPLine(barTime, volume, price, offset);
                }
            }
            vwapHistoryStatus = VWAPHistoryStatus.Processed;
        }

        private void UpdateVWAPLine(DateTime dateTime, double volume, double price, int offset)
        {
            this.calculator.CalculateVWAP(dateTime, volume, price);
            if (this.calculator.AllowSetVWAP(dateTime))
                this.Lines["VWAP"].SetValue(this.calculator.LastVWAPValue, offset);
        }
        private double GetLastPrice()
        {
            switch (this.HistoryDataSeries.HistoricalRequest.DataType)
            {
                case DataType.Bid:
                    return this.InstrumentsManager.Current.DayInfo.LastBBO.Bid;
                case DataType.Ask:
                    return this.InstrumentsManager.Current.DayInfo.LastBBO.Ask;
                case DataType.Trade:
                    return this.InstrumentsManager.Current.DayInfo.LastTrade.Price;
                default:
                    return double.NaN;
            }
        }
        private double GetLastSize()
        {
            if (!byTrades)
                return 1D;

            switch (this.HistoryDataSeries.HistoricalRequest.DataType)
            {
                case DataType.Bid:
                    return this.InstrumentsManager.Current.DayInfo.LastBBO.BidSize;
                case DataType.Ask:
                    return this.InstrumentsManager.Current.DayInfo.LastBBO.AskSize;
                case DataType.Trade:
                    return this.InstrumentsManager.Current.DayInfo.LastTrade.Size;
                default:
                    return double.NaN;
            }
        }
    }
    #endregion

    #region (OBV) OBV
    public class OBV : IndicatorBuilder
    {
        public OBV()
            : base()
        {
            Credentials.ProjectName = "On Balance Volume";

            Lines.Set("OBV");
            Lines["OBV"].Color = Color.DodgerBlue;

            base.SeparateWindow = true;
        }

        [InputParameter(InputType.Combobox, "Sources prices for OBV", 1)]
        [ComboboxItem("Close", PriceType.Close)]
        [ComboboxItem("Open", PriceType.Open)]
        [ComboboxItem("High", PriceType.High)]
        [ComboboxItem("Low", PriceType.Low)]
        [ComboboxItem("Typical", PriceType.Typical)]
        [ComboboxItem("Medium", PriceType.Medium)]
        [ComboboxItem("Weighted", PriceType.Weighted)]
        public PriceType SourcePrice = PriceType.Close;

        BuiltInIndicator obv;

        public override void Init()
        {
            ScriptShortName = "OBV";

            obv = IndicatorsManager.BuildIn.OBV(HistoryDataSeries, SourcePrice);
        }

        public override void Update(TickStatus args)
        {
            Lines["OBV"].SetValue(obv.GetValue());
        }
    }
    #endregion (OBV)OBV

    #region RVOL (Relative Volume)
    public class RVOL : IndicatorBuilder
    {
        public RVOL()
            : base()
        {
            #region Initialization
            Credentials.ProjectName = "Relative Volume";
            #endregion

            Lines.Set("volume_positive");
            Lines["volume_positive"].Color = Color.Teal;
            Lines["volume_positive"].Style = LineStyle.Histogram;

            Lines.Set("volume_negative");
            Lines["volume_negative"].Color = Color.Gray;
            Lines["volume_negative"].Style = LineStyle.Histogram;

            Lines.Set("ma");
            Lines["ma"].Color = Color.Orange;

            Lines.Set("rvol");
            Lines["rvol"].Color = Color.CadetBlue;

            Lines.Set("volume_threshold");
            Lines["volume_threshold"].Color = Color.Blue;

            Lines.Set("rvol_threshold");
            Lines["rvol_threshold"].Color = Color.LightGreen;

            Lines.Set("lookback_threshold");
            Lines["lookback_threshold"].Color = Color.Red;

            SeparateWindow = true;
        }

        [InputParameter(InputType.Numeric, "Highest Volume Lookback Period", 1)]
        [SimpleNumeric(1D, 99999D, 0, 1)]
        public int lookback = 50;

        [InputParameter(InputType.Numeric, "MA Length", 2)]
        [SimpleNumeric(1, 999, 2, 1)]
        public int maLength = 50;

        [InputParameter(InputType.Combobox, "MA Type", 3)]
        [ComboboxItem("EMA", MAType.EMA)]
        [ComboboxItem("SMA", MAType.SMA)]
        [ComboboxItem("HMA", MAType.HMA)]
        [ComboboxItem("WMA", MAType.WMA)]
        [ComboboxItem("DEMA", MAType.DEMA)]
        public MAType maType = MAType.EMA;

        [InputParameter(InputType.Numeric, "Absolute Volume Level To Trigger Signal", 4)]
        [SimpleNumeric(0, 999, 2, 1)]
        public int volTrigger = 0;

        [InputParameter(InputType.Numeric, "Absolute RVOL Level To Trigger Signal", 5)]
        [SimpleNumeric(0, 999, 2, 0.1)]
        public double rvolTrigger = 2.5;

        [InputParameter(InputType.Checkbox, "Use Static Volume For Alerts?", 6)]
        public bool useVol = false;

        [InputParameter(InputType.Checkbox, "Use RVOL For Alerts?", 7)]
        public bool useRVOL = false;

        [InputParameter(InputType.Checkbox, "Use Lookback For Alerts?", 8)]
        public bool useLookback = false;

        [InputParameter(InputType.Checkbox, "Draw RVOL Threshold?", 9)]
        public bool showRVOL = true;

        BuiltInIndicator ma, smoothMa, doubleSmoothMa;

        public override void Init()
        {
            if (maType == MAType.SMA)
            {
                ma = IndicatorsManager.BuildIn.MA((int index) => { return this.GetVolume(index); }, maLength, MAMode.SMA);
            }
            else if (maType == MAType.EMA)
            {
                ma = IndicatorsManager.BuildIn.MA((int index) => { return this.GetVolume(index); }, maLength, MAMode.EMA);
            }
            else if (maType == MAType.DEMA)
            {
                ma = IndicatorsManager.BuildIn.MA((int index) => { return this.GetVolume(index); }, maLength, MAMode.EMA);
                smoothMa = IndicatorsManager.BuildIn.MA((int index) => { return this.ma.GetValue(index); }, maLength, MAMode.EMA);
            }
            else if (maType == MAType.WMA)
            {
                ma = IndicatorsManager.BuildIn.MA((int index) => { return this.GetVolume(index); }, maLength, MAMode.LWMA);
            }
            else if (maType == MAType.HMA)
            {
                ma = IndicatorsManager.BuildIn.MA((int index) => { return this.GetVolume(index); }, maLength, MAMode.LWMA);
                smoothMa = IndicatorsManager.BuildIn.MA((int index) => { return this.GetVolume(index); }, maLength / 2, MAMode.LWMA);
                doubleSmoothMa = IndicatorsManager.BuildIn.MA(x => {
                    if (smoothMa.Count < x + 1)
                        return 0;
                    return 2.0 * smoothMa.GetValue(x + 1) - ma.GetValue(x + 1);
                }, (int)Math.Sqrt(maLength), MAMode.LWMA);
            }
        }
        public override void Complete()
        {
            ma = null;
            smoothMa = null;
            doubleSmoothMa = null;
        }
        public override void Update(TickStatus args)
        {
            if (HistoryDataSeries.Count < maLength)
                return;

            var _ma = GetMA();
            var rvol = _ma != 0 ? GetVolume() / _ma : 0D;

            // Generate volume alerts / signals
            var highestVolume = GetHighestVolume();
            var volAlert = useVol && GetVolume() >= volTrigger;
            var rvolAlert = useRVOL && rvol >= rvolTrigger;
            var lookbackAlert = useLookback && GetVolume() >= highestVolume;
            var alert = volAlert || rvolAlert || lookbackAlert;
            if (alert)
            {
                Lines["volume_positive"].SetValue(GetVolume());
                if (args != TickStatus.IsHistory && args != TickStatus.IsQuote)
                {
                    Notification.Alert($"VOL signal for {InstrumentsManager.Current.Symbol}");
                }
            }
            else
            {
                Lines["volume_negative"].SetValue(GetVolume());
            }
            Lines["ma"].SetValue(_ma);
            Lines["rvol"].SetValue(rvol);
            if (showRVOL && useVol)
            {
                Lines["volume_threshold"].SetValue(volTrigger);
            }
            if (showRVOL && useRVOL)
            {
                Lines["rvol_threshold"].SetValue(rvolTrigger * _ma);
            }
            if (showRVOL && useLookback)
            {
                Lines["lookback_threshold"].SetValue(highestVolume);
            }
        }
        private double GetVolume(int x = 0)
        {
            var vol = (HistoryDataSeries as BarData).GetVolume(x);

            return !double.IsNaN(vol) ? vol : (HistoryDataSeries as BarData).GetTicks(x);
        }

        private double GetHighestVolume()
        {
            var highest = 0D;
            var _lookback = Math.Min(lookback, HistoryDataSeries.Count - 1);
            for (int i = 0; i < _lookback; i++)
            {
                highest = GetVolume(i) > highest ? GetVolume(i) : highest;
            }
            return highest;
        }

        private double GetMA()
        {
            switch (this.maType)
            {

                case MAType.HMA:
                    return doubleSmoothMa.GetValue();
                case MAType.DEMA:
                    return 2 * ma.GetValue() - smoothMa.GetValue();
                case MAType.WMA:
                case MAType.EMA:
                default:
                    return ma.GetValue();
            }
        }
    }

    public enum MAType
    {
        EMA,
        SMA,
        HMA,
        WMA,
        DEMA
    }
    #endregion RVOLBS (Relative Volume Beasley Savage)
    public class RVOLBS : IndicatorBuilder
    {
        public RVOLBS()
        : base()
        {
            #region Initialization
            Credentials.ProjectName = "Relative Volume Beasley Savage";
            #endregion

            Lines.Set("rvol");
            Lines["rvol"].Color = Color.CadetBlue;

            SeparateWindow = true;
        }


        [InputParameter(InputType.Numeric, "Volume Length", 2)]
        [SimpleNumeric(1, 999, 2, 1)]
        public int volumeLength = 14;

        BuiltInIndicator averageVolume;

        public override void Init()
        {
            averageVolume = IndicatorsManager.BuildIn.MA((int index) => { return this.GetVolume(index); }, volumeLength, MAMode.SMA);
        }

        public override void Update(TickStatus args)
        {
            var curAverageVolume = averageVolume.GetValue();

            if (curAverageVolume != 0)
            {
                var relativeVolume = GetVolume() / curAverageVolume;
                Lines["rvol"].SetValue(relativeVolume);
            }
        }

        private double GetVolume(int x = 0)
        {
            var vol = (HistoryDataSeries as BarData).GetVolume(x);

            return !double.IsNaN(vol) ? vol : (HistoryDataSeries as BarData).GetTicks(x);
        }

        public override void Complete()
        {
            averageVolume = null;
        }
    }
    #region RVOLBS (Relative Volume Beasley Savage)
    #endregion
}
