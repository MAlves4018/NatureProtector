namespace NatureProtector.Shared.Contracts.Readings;

public enum SensorOperationalState
{
    Nominal = 0,
    Delayed = 1,
    Invalid = 2,
    Dropped = 3,
    Retransmitted = 4
}