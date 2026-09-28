using NpgsqlTypes;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;

namespace Prumo.Api.Configuration;

/// <summary>
/// Writes the event instant in UTC to the `raise_date` column.
///
/// The sink's `TimestampColumnWriter` passes the raw `LogEvent.Timestamp`, a
/// `DateTimeOffset` in the local time zone. Npgsql rejects any non-zero offset for
/// `timestamp with time zone`, so **every batch died** with
/// `Cannot write DateTimeOffset with Offset=-03:00:00`. The `logs` table existed and never
/// got a row — the exception happens inside Serilog's periodic batch, which swallows it
/// unless `SelfLog` is on.
///
/// `UtcDateTime` returns a `DateTime` with `Kind=Utc`, which is what Npgsql accepts.
/// </summary>
public sealed class UtcTimestampColumnWriter : ColumnWriterBase
{
    public UtcTimestampColumnWriter() : base(NpgsqlDbType.TimestampTz)
    {
    }

    public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null)
        => logEvent.Timestamp.UtcDateTime;
}
