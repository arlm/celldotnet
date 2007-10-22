using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CellDotNet
{
	static class ShellUtilities
	{
		internal static string ExecuteShellScript(string scriptText)
		{
			string scripttempfile = null;
			try
			{
				scripttempfile = Path.GetTempFileName();

				File.WriteAllText(scripttempfile, scriptText.Replace("\r\n", "\n"));

				return ExecuteCommandAndGetOutput("sh", scripttempfile);
			}
			finally
			{
				if (scripttempfile != null && File.Exists(scripttempfile))
					File.Delete(scripttempfile);
			}
		}

		internal static string ExecuteCommandAndGetOutput(string program, string arguments)
		{
			using (Process p = new Process())
			{
				p.StartInfo.FileName = program;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				if (!string.IsNullOrEmpty(arguments))
					p.StartInfo.Arguments = arguments;

				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;
				p.Start();
				StringBuilder sb = new StringBuilder();

				while (!p.HasExited)
				{
					sb.AppendLine(p.StandardOutput.ReadToEnd());

					if (p.StandardError.Peek() != -1)
					{
						string alloutput = p.StandardError.ReadToEnd();
						throw new ShellExecutionException(string.Format("The program wrote {0} characters to standard output:\r\n{1}",
							alloutput.Length, alloutput));
					}
				}

				return sb.ToString();
			}
		}
	}
}
