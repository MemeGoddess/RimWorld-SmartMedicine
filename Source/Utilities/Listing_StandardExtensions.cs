using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace TD.Utilities
{
	public static class Listing_StandardExtensions
	{
		public static void SliderLabeled(this Listing_Standard ls, string label, ref int val, string format, float min = 0f, float max = 100f, string tooltip = null)
		{
			float fVal = val;
			ls.SliderLabeled(label, ref fVal, format, min, max);
			val = (int)fVal;
		}
		public static void SliderLabeled(this Listing_Standard ls, string label, ref float val, string format, float min = 0f, float max = 1f, string tooltip = null)
		{
			Rect rect = ls.GetRect(Text.LineHeight);
			Rect rect2 = rect.LeftPart(.70f).Rounded();
			Rect rect3 = rect.RightPart(.30f).Rounded().LeftPart(.67f).Rounded();
			Rect rect4 = rect.RightPart(.10f).Rounded();

			TextAnchor anchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(rect2, label);

#pragma warning disable CS0612 // Type or member is obsolete
			float result = Widgets.HorizontalSlider(rect3, val, min, max, true);
#pragma warning restore CS0612 // Type or member is obsolete
			val = result;
			Text.Anchor = TextAnchor.MiddleRight;
			Widgets.Label(rect4, String.Format(format, val));
			if (!tooltip.NullOrEmpty())
			{
				TooltipHandler.TipRegion(rect, tooltip);
			}

			Text.Anchor = anchor;
			ls.Gap(ls.verticalSpacing);
		}

		public static FieldInfo rectInfo = AccessTools.Field(typeof(Listing_Standard), "listingRect");
		//listing.columnWidthInt = listing.listingRect.width;
		public static FieldInfo widthInfo = AccessTools.Field(typeof(Listing_Standard), "columnWidthInt");
		//listing.curX = 0f;
		public static FieldInfo curXInfo = AccessTools.Field(typeof(Listing_Standard), "curX");
		//listing.curY = 0f;
		public static FieldInfo curYInfo = AccessTools.Field(typeof(Listing_Standard), "curY");
		public static FieldInfo fontInfo = AccessTools.Field(typeof(Listing_Standard), "font");
		public static void BeginScrollViewEx(this Listing_Standard listing, Rect rect, ref Vector2 scrollPosition, Rect viewRect)
		{
			Widgets.BeginGroup(rect);
			Widgets.BeginScrollView(rect.AtZero(), ref scrollPosition, viewRect, true);

			rect.height = 100000f;
			rect.width -= 20f;

			rectInfo.SetValue(listing, rect);
			widthInfo.SetValue(listing, rect.width);
			curXInfo.SetValue(listing, 0);
			curYInfo.SetValue(listing, 0);

			Text.Font = (GameFont)fontInfo.GetValue(listing);
		}

		public static void EndScrollView(this Listing_Standard listing, ref Rect viewRect)
		{
			viewRect.width = listing.ColumnWidth;
			viewRect.height = listing.CurHeight;
			Widgets.EndScrollView();
			listing.End();
		}
	}
}