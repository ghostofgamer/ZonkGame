using TMPro;

namespace Zonk.Utils
{
    /// <summary>
    /// Число в TMP-текст без выделения памяти: «префикс + число + суффикс» собирается в общий буфер символов
    /// и отдаётся SetCharArray. Для счётчиков, которые меняются каждый кадр (накрутка очков).
    /// </summary>
    public static class NumberText
    {
        private static readonly char[] Buffer = new char[512];
        private static readonly char[] Digits = new char[12];

        public static void Set(TMP_Text text, string prefix, int value, string suffix = null)
        {
            if (text == null)
                return;

            var length = 0;
            length = Append(prefix, length);

            if (value < 0 && length < Buffer.Length)
            {
                Buffer[length++] = '-';
                value = -value;
            }

            var count = 0;
            do
            {
                Digits[count++] = (char)('0' + value % 10);
                value /= 10;
            } while (value > 0 && count < Digits.Length);

            while (count > 0 && length < Buffer.Length)
                Buffer[length++] = Digits[--count];

            length = Append(suffix, length);
            text.SetCharArray(Buffer, 0, length);
        }

        private static int Append(string value, int length)
        {
            if (string.IsNullOrEmpty(value))
                return length;

            for (var i = 0; i < value.Length && length < Buffer.Length; i++)
                Buffer[length++] = value[i];
            return length;
        }
    }
}
