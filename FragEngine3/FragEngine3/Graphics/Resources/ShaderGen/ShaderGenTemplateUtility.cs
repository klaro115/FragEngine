using FragEngine3.EngineCore;
using System.Text;

namespace FragEngine3.Graphics.Resources.ShaderGen
{
	public static class ShaderGenTemplateUtility
	{
		#region Methods

		public static bool CreateCodeFromTemplate(string _templateCode, string[] _templateNames, StringBuilder _templateBuilder, IList<string> _templateReplacements)
		{
			if (_templateBuilder == null)
			{
				Logger.Instance?.LogError("Cannot create code from template using null string builder!");
				return false;
			}
			_templateBuilder.Clear();

			// No code, do nothing:
			if (string.IsNullOrEmpty(_templateCode)) return true;

			// No template names? Code is probably not templated, return code as-is:
			if (_templateNames == null || _templateNames.Length == 0)
			{
				_templateBuilder.Append(_templateCode);
				return true;
			}

			// Check if a sufficient number of replacements were provided for the templated names at hand:
			if (_templateReplacements == null || _templateNames!.Length > _templateReplacements.Count)
			{
				Logger.Instance?.LogError($"Cannot create code from templates using insufficient template name replacements!");
				return false;
			}
			_templateBuilder ??= new StringBuilder(_templateCode.Length);

			// Substitute any templated code with the corresponding replacement code blocks:
			_templateBuilder.Clear();
			_templateBuilder.Append(_templateCode);
			for (int i = 0; i < _templateNames.Length; ++i)
			{
				string templateName = _templateNames[i];
				string replacement = _templateReplacements[i];

				if (!string.IsNullOrEmpty(templateName) &&
					!string.IsNullOrEmpty(replacement))
				{
					_templateBuilder.Replace(templateName, replacement);
				}
			}
			_templateBuilder.Append(_templateBuilder);

			return true;
		}

		#endregion
	}
}
