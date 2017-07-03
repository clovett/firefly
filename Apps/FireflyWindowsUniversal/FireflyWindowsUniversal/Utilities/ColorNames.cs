using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace FireflyWindows.Utilities
{
    class ColorNames
    {
        public static Color ParseColor(string colorNameorHex)
        {
            if (string.IsNullOrEmpty(colorNameorHex))
            {
                return Colors.Black;
            }
            if (colorNameorHex.StartsWith("#"))
            {
                return ParseHexColor(colorNameorHex);
            }
            var map = GetColorMap();
            string name = colorNameorHex.Trim();
            string color = null;
            if (map.TryGetValue(name, out color))
            {
                return ParseHexColor(color);
            }
            return Colors.Black;
        }

        public static Color ParseHexColor(string hex)
        {
            hex = hex.Replace("#", string.Empty);
            byte a = 0xff;
            byte r = 0, g = 0, b = 0;
            if (hex.Length == 8)
            {
                a = (byte)(System.Convert.ToUInt32(hex.Substring(0, 2), 16));
                hex = hex.Substring(2);
            }
            if (hex.Length == 6)
            {
                r = (byte)(System.Convert.ToUInt32(hex.Substring(0, 2), 16));
                hex = hex.Substring(2);
            }
            if (hex.Length == 4)
            {
                g = (byte)(System.Convert.ToUInt32(hex.Substring(0, 2), 16));
                hex = hex.Substring(2);
            }
            if (hex.Length == 2)
            {
                b = (byte)(System.Convert.ToUInt32(hex.Substring(0, 2), 16));
                hex = hex.Substring(2);
            }
            var color = Windows.UI.Color.FromArgb(a, r, g, b);
            return color;
        }

        public static Dictionary<string, string> GetColorMap()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            map["IndianRed"] = "#CD5C5C";
            map["LightCoral"] = "#F08080";
            map["Salmon"] = "#FA8072";
            map["DarkSalmon"] = "#E9967A";
            map["LightSalmon"] = "#FFA07A";
            map["Crimson"] = "#DC143C";
            map["Red"] = "#FF0000";
            map["FireBrick"] = "#B22222";
            map["DarkRed"] = "#8B0000";
            map["Pink"] = "#FFC0CB";
            map["LightPink"] = "#FFB6C1";
            map["HotPink"] = "#FF69B4";
            map["DeepPink"] = "#FF1493";
            map["MediumVioletRed"] = "#C71585";
            map["PaleVioletRed"] = "#DB7093";
            map["LightSalmon"] = "#FFA07A";
            map["Coral"] = "#FF7F50";
            map["Tomato"] = "#FF6347";
            map["OrangeRed"] = "#FF4500";
            map["DarkOrange"] = "#FF8C00";
            map["Orange"] = "#FFA500";
            map["Gold"] = "#FFD700";
            map["Yellow"] = "#FFFF00";
            map["LightYellow"] = "#FFFFE0";
            map["LemonChiffon"] = "#FFFACD";
            map["LightGoldenrodYellow"] = "#FAFAD2";
            map["PapayaWhip"] = "#FFEFD5";
            map["Moccasin"] = "#FFE4B5";
            map["PeachPuff"] = "#FFDAB9";
            map["PaleGoldenrod"] = "#EEE8AA";
            map["Khaki"] = "#F0E68C";
            map["DarkKhaki"] = "#BDB76B";
            map["Lavender"] = "#E6E6FA";
            map["Thistle"] = "#D8BFD8";
            map["Plum"] = "#DDA0DD";
            map["Violet"] = "#EE82EE";
            map["Orchid"] = "#DA70D6";
            map["Fuchsia"] = "#FF00FF";
            map["Magenta"] = "#FF00FF";
            map["MediumOrchid"] = "#BA55D3";
            map["MediumPurple"] = "#9370DB";
            map["RebeccaPurple"] = "#663399";
            map["BlueViolet"] = "#8A2BE2";
            map["DarkViolet"] = "#9400D3";
            map["DarkOrchid"] = "#9932CC";
            map["DarkMagenta"] = "#8B008B";
            map["Purple"] = "#800080";
            map["Indigo"] = "#4B0082";
            map["SlateBlue"] = "#6A5ACD";
            map["DarkSlateBlue"] = "#483D8B";
            map["MediumSlateBlue"] = "#7B68EE";
            map["GreenYellow"] = "#ADFF2F";
            map["Chartreuse"] = "#7FFF00";
            map["LawnGreen"] = "#7CFC00";
            map["Lime"] = "#00FF00";
            map["LimeGreen"] = "#32CD32";
            map["PaleGreen"] = "#98FB98";
            map["LightGreen"] = "#90EE90";
            map["MediumSpringGreen"] = "#00FA9A";
            map["SpringGreen"] = "#00FF7F";
            map["MediumSeaGreen"] = "#3CB371";
            map["SeaGreen"] = "#2E8B57";
            map["ForestGreen"] = "#228B22";
            map["Green"] = "#008000";
            map["DarkGreen"] = "#006400";
            map["YellowGreen"] = "#9ACD32";
            map["OliveDrab"] = "#6B8E23";
            map["Olive"] = "#808000";
            map["DarkOliveGreen"] = "#556B2F";
            map["MediumAquamarine"] = "#66CDAA";
            map["DarkSeaGreen"] = "#8FBC8B";
            map["LightSeaGreen"] = "#20B2AA";
            map["DarkCyan"] = "#008B8B";
            map["Teal"] = "#008080";
            map["Aqua"] = "#00FFFF";
            map["Cyan"] = "#00FFFF";
            map["LightCyan"] = "#E0FFFF";
            map["PaleTurquoise"] = "#AFEEEE";
            map["Aquamarine"] = "#7FFFD4";
            map["Turquoise"] = "#40E0D0";
            map["MediumTurquoise"] = "#48D1CC";
            map["DarkTurquoise"] = "#00CED1";
            map["CadetBlue"] = "#5F9EA0";
            map["SteelBlue"] = "#4682B4";
            map["LightSteelBlue"] = "#B0C4DE";
            map["PowderBlue"] = "#B0E0E6";
            map["LightBlue"] = "#ADD8E6";
            map["SkyBlue"] = "#87CEEB";
            map["LightSkyBlue"] = "#87CEFA";
            map["DeepSkyBlue"] = "#00BFFF";
            map["DodgerBlue"] = "#1E90FF";
            map["CornflowerBlue"] = "#6495ED";
            map["MediumSlateBlue"] = "#7B68EE";
            map["RoyalBlue"] = "#4169E1";
            map["Blue"] = "#0000FF";
            map["MediumBlue"] = "#0000CD";
            map["DarkBlue"] = "#00008B";
            map["Navy"] = "#000080";
            map["MidnightBlue"] = "#191970";
            map["Cornsilk"] = "#FFF8DC";
            map["BlanchedAlmond"] = "#FFEBCD";
            map["Bisque"] = "#FFE4C4";
            map["NavajoWhite"] = "#FFDEAD";
            map["Wheat"] = "#F5DEB3";
            map["BurlyWood"] = "#DEB887";
            map["Tan"] = "#D2B48C";
            map["RosyBrown"] = "#BC8F8F";
            map["SandyBrown"] = "#F4A460";
            map["Goldenrod"] = "#DAA520";
            map["DarkGoldenrod"] = "#B8860B";
            map["Peru"] = "#CD853F";
            map["Chocolate"] = "#D2691E";
            map["SaddleBrown"] = "#8B4513";
            map["Sienna"] = "#A0522D";
            map["Brown"] = "#A52A2A";
            map["Maroon"] = "#800000";
            map["White"] = "#FFFFFF";
            map["Snow"] = "#FFFAFA";
            map["HoneyDew"] = "#F0FFF0";
            map["MintCream"] = "#F5FFFA";
            map["Azure"] = "#F0FFFF";
            map["AliceBlue"] = "#F0F8FF";
            map["GhostWhite"] = "#F8F8FF";
            map["WhiteSmoke"] = "#F5F5F5";
            map["SeaShell"] = "#FFF5EE";
            map["Beige"] = "#F5F5DC";
            map["OldLace"] = "#FDF5E6";
            map["FloralWhite"] = "#FFFAF0";
            map["Ivory"] = "#FFFFF0";
            map["AntiqueWhite"] = "#FAEBD7";
            map["Linen"] = "#FAF0E6";
            map["LavenderBlush"] = "#FFF0F5";
            map["MistyRose"] = "#FFE4E1";
            map["Gainsboro"] = "#DCDCDC";
            map["LightGray"] = "#D3D3D3";
            map["Silver"] = "#C0C0C0";
            map["DarkGray"] = "#A9A9A9";
            map["Gray"] = "#808080";
            map["DimGray"] = "#696969";
            map["LightSlateGray"] = "#778899";
            map["SlateGray"] = "#708090";
            map["DarkSlateGray"] = "#2F4F4F";
            map["Black"] = "#000000";
            return map;
        }
    }
}
