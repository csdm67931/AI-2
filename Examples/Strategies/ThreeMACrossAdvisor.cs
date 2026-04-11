﻿using Runtime.Script;
using TradeApi.Indicators;
using TradeApi.Trading;

namespace ThreeMACrossAdvisor
{
    public class ThreeMACrossAdvisor : StrategyBuilder
    {
        [InputParameter(InputType.Numeric, "Short Moving Average Period", 0)]
        [SimpleNumeric(1D, 100D)]
        public int ShortMaPeriod = 5;

        [InputParameter(InputType.Numeric, "Middle Moving Average Period", 1)]
        [SimpleNumeric(1D, 100D)]
        public int MiddleMaPeriod = 10;

        [InputParameter(InputType.Numeric, "Long Moving Average Period", 2)]
        [SimpleNumeric(1D, 100D)]
        public int LongMaPeriod = 25;

        private State state;
        private double amount;

        private BuiltInIndicator maSignal;
        private string orderId;

        public ThreeMACrossAdvisor()
            : base()
        {
            Credentials.Password = "66b4a6416f59370e942d353f08a9ae36";
            Credentials.ProjectName = "3MACrossAdvisor";
        }

        public override void Init()
        {
            state = State.ExitMarket;
            amount = 1;
            maSignal = IndicatorsManager.BuildIn.Custom("3MASignal", HistoryDataSeries, ShortMaPeriod, MiddleMaPeriod, LongMaPeriod, 1);
        }

        public override void Update(TickStatus args)
        {
            Trend trend = (Trend)maSignal.GetValue();

            switch (trend)
            {
                case Trend.Up:
                    if (state == State.EnteredShort)
                    {
                        var positions = PositionsManager.GetPositions(x => x.ID == orderId);
                        if (positions.Count == 1 && PositionsManager.Close(positions[0]))
                            state = State.ExitMarket;
                        return;
                    }

                    if (state != State.EnteredBuy)
                    {
                        double ask = InstrumentsManager.Current.DayInfo.Ask;

                        if (double.IsNaN(ask))
                            return;

                        OrderRequest request = new OrderRequest(OrderType.Market, InstrumentsManager.Current, AccountManager.Current, OrderSide.Buy, amount, ask);
                        OrdersManager.SendAsync(request, result => {
                            orderId = result.Id;
                            state = State.EnteredBuy;
                        });
                        state = State.EnteredBuy;
                    }
                    break;

                case Trend.Down:
                    if (state == State.EnteredBuy)
                    {

                        var positions = PositionsManager.GetPositions(x => x.ID == orderId);
                        if (positions.Count == 1 && PositionsManager.Close(positions[0]))
                            state = State.ExitMarket;
                        return;
                    }

                    if (state != State.EnteredShort)
                    {
                        double bid = InstrumentsManager.Current.DayInfo.Bid;

                        if (double.IsNaN(bid))
                            return;

                        OrderRequest request = new OrderRequest(OrderType.Market, InstrumentsManager.Current, AccountManager.Current, OrderSide.Sell, amount, bid);
                        OrdersManager.SendAsync(request, result => {
                            orderId = result.Id;
                            state = State.EnteredShort;
                        });
                        state = State.EnteredShort;
                    }
                    break;

            }
        }

        public override void Complete()
        { }
    }

    internal enum Trend
    {
        Up = 1,
        No = 0,
        Down = -1,
    }

    internal enum State
    {
        EnteredBuy = 1,
        EnteredShort = -1,
        ExitMarket = 0,
    }
}
