namespace EscLang.Parse;

internal enum OperatorPriority : Int32
{
	Invalid = 0,

	// lowest priority

	Declaration,

	Assignment,

	Comma,

	// comparison: equality

	EqualTo,

	NotEqualTo = EqualTo,

	// comparison: inequality

	LessThan,

	MoreThan = LessThan,

	LessThanOrEqualTo = LessThan,
	
	MoreThanOrEqualTo = LessThan,

	// math: addition and subtractions

	Plus,

	Minus = Plus,

	// math: multiplication and division

	Multiply,

	Divide = Multiply,

	// highest priority

	Call,

	MemberAccess = Call,
}