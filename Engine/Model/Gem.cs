namespace MatchThree.Engine.Model;

public readonly record struct Gem(int Id, GemType Type, Special? Special = null);