namespace EscLang.Parse;

internal enum OperatorPriority : Int32
{
	Invalid = 0,

	// lowest priority

	Declaration,

	Assignment,

	// comparison: equality

	EqualTo,

	NotEqualTo = EqualTo,

	// math: addition and subtractions

	Plus,

	Minus = Plus,

	// math: multiplication and division

	Multiply,

	Divide = Multiply,

	// highest priority

	Call,

	Dereference,
}