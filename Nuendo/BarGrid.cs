namespace MgaWwiseIMImporter.Nuendo;

internal static class BarGrid
{
    /// <summary>
    /// N/D 拍子の1小節長 (PPQ)。1拍 = 4/D 四分音符、1小節 = N 拍。
    /// </summary>
    public static double BarLengthPpq(int numerator, int denominator)
    {
        if (numerator <= 0 || denominator <= 0)
        {
            return NuendoTracklistInfo.PulsesPerQuarterNote * 4d;
        }

        return NuendoTracklistInfo.PulsesPerQuarterNote * numerator * 4d / denominator;
    }

    /// <summary>
    /// untilPpq までの小節線 PPQ を返す（その拍子区間をはみ出さない）。
    /// テンポ変化では切らない。拍子変更で小節長だけ切り替える。
    /// </summary>
    public static IReadOnlyList<double> GetBarBoundaries(
        IReadOnlyList<NuendoSignatureEvent> signatures,
        double untilPpq)
    {
        var bounds = new SortedSet<double> { 0d };
        if (untilPpq < 0)
        {
            return bounds.ToList();
        }

        if (signatures.Count == 0)
        {
            AddBarsThrough(bounds, startPpq: 0d, untilPpq: untilPpq, numerator: 4, denominator: 4);
            return bounds.ToList();
        }

        for (var i = 0; i < signatures.Count; i++)
        {
            var signature = signatures[i];
            if (signature.Ppq > untilPpq + 1e-9)
            {
                break;
            }

            bounds.Add(signature.Ppq);

            // 次の拍子変更、または until まで。前の拍子の尺ではみ出さない。
            var segmentLimit = untilPpq;
            if (i + 1 < signatures.Count)
            {
                segmentLimit = Math.Min(untilPpq, signatures[i + 1].Ppq);
            }

            AddBarsThrough(
                bounds,
                startPpq: signature.Ppq,
                untilPpq: segmentLimit,
                numerator: signature.Numerator,
                denominator: signature.Denominator);
        }

        return bounds.ToList();
    }

    public static double? FindPreviousBarPpq(IReadOnlyList<double> barBoundaries, double ppq)
    {
        double? previous = null;
        foreach (var barPpq in barBoundaries)
        {
            if (barPpq > ppq + 1e-9)
            {
                break;
            }

            previous = barPpq;
        }

        return previous;
    }

    public static double? FindNextBarPpq(IReadOnlyList<double> barBoundaries, double ppq)
    {
        foreach (var barPpq in barBoundaries)
        {
            if (barPpq > ppq + 1e-9)
            {
                return barPpq;
            }
        }

        return null;
    }

    /// <summary>
    /// <paramref name="ppq"/> がいずれかの小節線から <paramref name="epsilon"/> 以内なら、その線へスナップする。
    /// </summary>
    public static double SnapToNearbyBarPpq(
        IReadOnlyList<double> barBoundaries,
        double ppq,
        double epsilon)
    {
        double? nearest = null;
        var nearestDistance = double.MaxValue;
        foreach (var barPpq in barBoundaries)
        {
            var distance = Math.Abs(barPpq - ppq);
            if (distance <= epsilon && distance < nearestDistance)
            {
                nearest = barPpq;
                nearestDistance = distance;
            }
        }

        return nearest ?? ppq;
    }

    /// <summary>
    /// 候補 PPQ が values のいずれかに十分近いか（同一小節線判定など）。
    /// </summary>
    public static bool IsNearAny(IReadOnlyList<double> values, double ppq, double epsilon = 1e-6)
    {
        foreach (var value in values)
        {
            if (Math.Abs(value - ppq) <= epsilon)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddBarsThrough(
        SortedSet<double> bounds,
        double startPpq,
        double untilPpq,
        int numerator,
        int denominator)
    {
        var barLength = BarLengthPpq(numerator, denominator);
        if (barLength <= 0)
        {
            return;
        }

        for (var barIndex = 0; ; barIndex++)
        {
            var ppq = startPpq + (barIndex * barLength);
            // until を超える線は足さない（旧実装は「直後の1本」を足し、拍子変更直後に誤線が出ていた）
            if (ppq > untilPpq + 1e-9)
            {
                break;
            }

            bounds.Add(ppq);
            if (barIndex > 1_000_000)
            {
                break;
            }
        }
    }
}
