namespace WarGame.Core.Stats;

public enum ModOp { Add, Mul }

/// <summary>Linha da tabela modifier. Condição: chave/valor do contexto do combate (terrain, air_sup, tech…).</summary>
public sealed record Modifier(
    int Id,
    string SourceKind,
    string? ConditionKey,
    string? ConditionValue,
    string StatKey,          // ex: "str_attacker"
    string? RequiredTag,     // ex: "armored" — só aplica se a divisão tiver a tag
    ModOp Op,
    float Value);
