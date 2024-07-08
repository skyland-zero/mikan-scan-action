using System;
using System.Text.RegularExpressions;

namespace MikanScan.ConsoleApp.Extensions;

public static class StringExtension
{
    /// <summary>
    /// 判断一个字符串是否为url
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static bool IsUrl(this string str)
    {
        try
        {
            string urlFormat = @"^http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?$";
            return Regex.IsMatch(str, urlFormat);
        }
        catch (Exception)
        {
            return false;
        }
    }
}