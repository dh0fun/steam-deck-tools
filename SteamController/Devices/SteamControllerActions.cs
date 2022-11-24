using hidapi;
using PowerControl.External;
using static CommonHelpers.Log;

namespace SteamController.Devices
{
    public partial class SteamController
    {
        public abstract class SteamAction
        {
            public String Name { get; internal set; } = "";

            /// This is action controlled by Lizard mode
            public bool LizardButton { get; set; }
            public bool LizardMouse { get; set; }
            public DateTime LastUpdated { get; protected set; } = DateTime.Now;
            public double DeltaTime { get; protected set; }

            internal abstract void Reset();
            internal abstract bool BeforeUpdate(byte[] buffer, SteamController controller);
            internal abstract void Update();

            protected void UpdateTime()
            {
                var now = DateTime.Now;
                DeltaTime = (now - LastUpdated).TotalSeconds;
                LastUpdated = now;
            }

            protected bool UsedByLizard(SteamController controller)
            {
                if (LizardButton && controller.LizardButtons)
                    return true;
                if (LizardMouse && controller.LizardMouse)
                    return true;
                return false;
            }
        }

        public class SteamButton : SteamAction
        {
            public static readonly TimeSpan DefaultHoldDuration = TimeSpan.FromMilliseconds(10);
            public static readonly TimeSpan DefaultFirstHold = TimeSpan.FromMilliseconds(75);
            public static readonly TimeSpan DefaultRepeatHold = TimeSpan.FromMilliseconds(150);

            public bool Value { get; private set; }
            public bool LastValue { get; private set; }

            /// Last press was already consumed by other
            public object? Consumed { get; private set; }

            /// Set on raising edge
            public DateTime? HoldSince { get; private set; }
            public DateTime? HoldRepeated { get; private set; }

            public SteamButton()
            {
            }

            public static implicit operator bool(SteamButton button) => button.Hold(DefaultHoldDuration, null);

            /// Generated when button is pressed for the first time
            public bool JustPressed()
            {
                if (!LastValue && Value)
                    return true;
                return false;
            }

            /// Generated on failing edge of key press
            public bool Pressed(TimeSpan? duration = null)
            {
                // We expect Last to be true, and now to be false (failing edge)
                if (!(LastValue && !Value))
                    return false;

                if (Consumed is not null)
                    return false;

                if (duration.HasValue && HoldSince?.Add(duration.Value) >= DateTime.Now)
                    return false;

                return true;
            }

            public bool Consume(object consume)
            {
                if (Consumed is not null && Consumed != consume)
                    return false;

                Consumed = consume;
                return true;
            }

            public bool Hold(object? consume = null)
            {
                return Hold(null, consume);
            }

            /// Generated when button was hold for a given period
            public bool Hold(TimeSpan? duration, object? consume = null)
            {
                if (!Value)
                    return false;

                if (Consumed is not null && Consumed != consume)
                    return false;

                if (duration.HasValue && HoldSince?.Add(duration.Value) >= DateTime.Now)
                    return false;

                if (consume is not null)
                    Consumed = consume;

                return true;
            }

            public bool HoldOnce(object consume)
            {
                return HoldOnce(null, consume);
            }

            /// Generated when button was hold for a given period
            /// but triggered exactly once
            public bool HoldOnce(TimeSpan? duration, object consume)
            {
                if (!Hold(duration))
                    return false;

                Consumed = consume;
                return true;
            }

            /// Generated when button was repeated for a given period
            /// but triggered exactly once
            public bool HoldRepeat(TimeSpan duration, TimeSpan repeatEvery, object consume)
            {
                // always generate at least one keypress
                if (Pressed(duration))
                    return true;

                if (!Hold(duration, consume))
                    return false;

                // first keypress
                if (!HoldRepeated.HasValue)
                {
                    HoldRepeated = DateTime.Now;
                    return true;
                }

                // repeated keypress
                if (HoldRepeated.Value.Add(repeatEvery) <= DateTime.Now)
                {
                    HoldRepeated = DateTime.Now;
                    return true;
                }

                return false;
            }

            public bool HoldRepeat(object consume)
            {
                return HoldRepeat(DefaultFirstHold, DefaultRepeatHold, consume);
            }

            internal override void Reset()
            {
                LastValue = Value;
                Value = false;
                HoldSince = null;
                HoldRepeated = null;
                Consumed = null;
            }

            internal void SetValue(bool value)
            {
                LastValue = Value;
                Value = value;
                UpdateTime();

                if (!LastValue && Value)
                {
                    HoldSince = DateTime.Now;
                    HoldRepeated = null;
                }
            }

            internal override bool BeforeUpdate(byte[] buffer, SteamController controller)
            {
                return true;
            }

            internal override void Update()
            {
                if (!Value)
                    Consumed = null;
            }
        }

        public class SteamButton2 : SteamButton
        {
            private int offset;
            private uint mask;

            public SteamButton2(int offset, object mask)
            {
                this.offset = offset;
                this.mask = (uint)mask.GetHashCode();

                while (this.mask > 0xFF)
                {
                    this.mask >>= 8;
                    this.offset++;
                }
            }

            internal override bool BeforeUpdate(byte[] buffer, SteamController controller)
            {
                if (UsedByLizard(controller))
                    return false;

                if (offset < buffer.Length)
                {
                    SetValue((buffer[offset] & mask) != 0);
                    return true;
                }
                else
                {
                    SetValue((buffer[offset] & mask) != 0);
                    return false;
                }
            }
        }

        public class SteamAxis : SteamAction
        {
            public const short VirtualLeftThreshold = short.MinValue / 2;
            public const short VirtualRightThreshold = short.MaxValue / 2;

            private int offset;

            public SteamButton? ActiveButton { get; internal set; }
            public SteamButton? VirtualLeft { get; internal set; }
            public SteamButton? VirtualRight { get; internal set; }
            public short Value { get; private set; }
            public short LastValue { get; private set; }
            public short Deadzone { get; set; }
            public short MinChange { get; set; }

            public SteamAxis(int offset)
            {
                this.offset = offset;
            }

            public static implicit operator bool(SteamAxis button) => button.Active;
            public static implicit operator short(SteamAxis button) => button.Value;

            public bool Active
            {
                get
                {
                    return ActiveButton?.Value ?? true;
                }
            }

            public enum ScaledMode
            {
                Absolute,
                AbsoluteTime,
                Delta,
                DeltaTime
            }

            public double Scaled(double min, double max, ScaledMode mode)
            {
                int value = 0;

                switch (mode)
                {
                    case ScaledMode.Absolute:
                        if (Math.Abs(Value) < Deadzone)
                            return 0.0;
                        value = Value;
                        break;

                    case ScaledMode.AbsoluteTime:
                        if (Math.Abs(Value) < Deadzone)
                            return 0.0;
                        value = (int)(Value * DeltaTime);
                        break;

                    case ScaledMode.Delta:
                        value = Value - LastValue;
                        if (Math.Abs(Value) < MinChange)
                            return 0.0;
                        break;

                    case ScaledMode.DeltaTime:
                        value = Value - LastValue;
                        if (Math.Abs(Value) < MinChange)
                            return 0.0;
                        value = (int)(value * DeltaTime);
                        break;
                }

                if (value == 0)
                    return 0.0;

                double factor = (double)(value - short.MinValue) / (short.MaxValue - short.MinValue);
                return factor * (max - min) + min;
            }

            public double Scaled(double range, ScaledMode mode)
            {
                return Scaled(-range, range, mode);
            }

            public int Scaled(int min, int max, ScaledMode mode)
            {
                return (int)Scaled((double)min, (double)max, mode);
            }

            public int Scaled(int range, ScaledMode mode)
            {
                return Scaled(-range, range, mode);
            }

            internal override void Reset()
            {
                LastValue = Value;
                Value = 0;
            }

            internal void SetValue(short value)
            {
                LastValue = Value;
                Value = value;
                UpdateTime();

                // first time pressed, reset value as this is a Pad
                if (ActiveButton is not null && ActiveButton.JustPressed())
                    LastValue = Value;

                if (VirtualRight is not null)
                    VirtualRight.SetValue(value > VirtualRightThreshold);

                if (VirtualLeft is not null)
                    VirtualLeft.SetValue(value < VirtualLeftThreshold);
            }

            internal override bool BeforeUpdate(byte[] buffer, SteamController controller)
            {
                if (UsedByLizard(controller))
                    return false;

                if (offset + 1 < buffer.Length)
                {
                    SetValue(BitConverter.ToInt16(buffer, offset));
                    return true;
                }
                else
                {
                    SetValue(0);
                    return false;
                }
            }

            internal override void Update()
            {
            }
        }

        public SteamAction?[] AllActions { get; private set; }
        public SteamButton?[] AllButtons { get; private set; }
        public SteamAxis?[] AllAxises { get; private set; }

        public IEnumerable<SteamButton> HoldButtons
        {
            get
            {
                foreach (var action in AllButtons)
                {
                    if (action.Value)
                        yield return action;
                }
            }
        }

        private void InitializeActions()
        {
            var allActions = GetType().
                GetFields().
                Where((field) => field.FieldType.IsSubclassOf(typeof(SteamAction))).
                Select((field) => Tuple.Create(field, field.GetValue(this) as SteamAction)).
                ToList();

            allActions.ForEach((tuple) => tuple.Item2.Name = tuple.Item1.Name);

            AllActions = allActions.Select((tuple) => tuple.Item2).ToArray();
            AllAxises = allActions.Where((tuple) => tuple.Item2 is SteamAxis).Select((tuple) => tuple.Item2 as SteamAxis).ToArray();
            AllButtons = allActions.Where((tuple) => tuple.Item2 is SteamButton).Select((tuple) => tuple.Item2 as SteamButton).ToArray();
        }

        public IEnumerable<string> GetReport()
        {
            List<string> report = new List<string>();

            var buttons = AllButtons.Where((button) => button.Value).Select((button) => button.Name);
            if (buttons.Any())
                yield return String.Format("Buttons: {0}", String.Join(",", buttons));

            foreach (var axis in AllAxises)
            {
                if (!axis.Active)
                    continue;
                yield return String.Format("Axis: {0} = {1} [Delta: {2}]", axis.Name, axis.Value, axis.Value - axis.LastValue);
            }
        }
    }
}