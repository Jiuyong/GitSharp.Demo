using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace GitSharp.Demo
{
	public static class Extension
	{
		public static bool GoToChinese(this ContentControl cc)
		{
			return VisualStateManager.GoToElementState(cc.Content as FrameworkElement, "Chinese", false);
		}
	}
}
