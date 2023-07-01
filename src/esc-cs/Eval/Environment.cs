using System.Text;

namespace EscLang.Eval;

public class Environment
{
	public readonly StringWriter ProgramOutput;

	public Environment(StringWriter programOutput)
	{
		this.ProgramOutput = programOutput;
	}
}
