using NpgsqlTypes;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;

namespace Prumo.Api.Configuration;

/// <summary>
/// Escreve o instante do evento em UTC na coluna `raise_date`.
///
/// O `TimestampColumnWriter` do sink entrega o `LogEvent.Timestamp` cru, que é um
/// `DateTimeOffset` no fuso local. O Npgsql recusa qualquer offset diferente de zero em
/// `timestamp with time zone`, então **todo batch morria** com
/// `Cannot write DateTimeOffset with Offset=-03:00:00`. A tabela `logs` existia e nunca
/// recebeu uma linha — a exceção acontece dentro do batch periódico do Serilog, que a
/// engole a menos que o `SelfLog` esteja ligado.
///
/// `UtcDateTime` devolve um `DateTime` com `Kind=Utc`, que é o que o Npgsql aceita.
/// </summary>
public sealed class UtcTimestampColumnWriter : ColumnWriterBase
{
    public UtcTimestampColumnWriter() : base(NpgsqlDbType.TimestampTz)
    {
    }

    public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null)
        => logEvent.Timestamp.UtcDateTime;
}
