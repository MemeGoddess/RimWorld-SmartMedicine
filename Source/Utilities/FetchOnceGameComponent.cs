using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace SmartMedicine.Utilities
{
	public abstract class FetchOnceGameComponent<T> : GameComponent where T : GameComponent
	{
		private static T _instance;
		public static T GetComp()
		{
			_instance ??= Current.Game.GetComponent<T>();
			return _instance;
		}

		internal static void ClearCache()
		{
			_instance = null;
		}
	}

	[HarmonyPatch(typeof(Game), nameof(Game.ClearCaches))]
	internal static class FetchOnceGameComponent_ClearCachePatch
	{
		private static List<Action> _clearActions;

		private static List<Action> BuildClearActions()
		{
			var actions = new List<Action>();
			var baseType = typeof(FetchOnceGameComponent<>);
			foreach (var type in GenTypes.AllSubclassesNonAbstract(typeof(GameComponent)))
			{
				var cur = type.BaseType;
				while (cur != null && cur != typeof(object))
				{
					if (cur.IsGenericType && cur.GetGenericTypeDefinition() == baseType)
					{
						var clearMethod = cur.GetMethod("ClearCache",
							System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
						if (clearMethod != null)
							actions.Add((Action)Delegate.CreateDelegate(typeof(Action), clearMethod));
						break;
					}
					cur = cur.BaseType;
				}
			}
			return actions;
		}

		public static void Prefix()
		{
			_clearActions ??= BuildClearActions();
			foreach (var clear in _clearActions)
				clear();
		}
	}
}
