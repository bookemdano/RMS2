using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

namespace Normalize
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            const int trials = 10;
            const int min = -100;
            const int max = 100;
            var gens = new List<NormalRandomGenerator>();
            for (int j = 0; j < trials; j++)
                gens.Add(new NormalRandomGenerator(min, max, (j + 1)*5));

            var hist = new Dictionary<int, int[]>();
            for (int i = min; i < max; i++)
                hist.Add(i, new int[trials]);

            for (int i = 0; i < 1E5; i++)
            {
                for (int j = 0; j < trials; j++)
                {
                    var rnd = (int)gens[j].Next();
                    hist[rnd][j]++;
                }
            }
            var outs = new List<string>();
            foreach(var kvp in hist)
            {
                outs.Add(kvp.Key + "," + string.Join(",", kvp.Value));
            }
            //var outs = hist.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Key + "," + kvp.Value);
            File.WriteAllLines(@"hist.csv", outs.ToArray());
        }
    }

    public class NormalRandomGenerator
    {
        private int Min { get; set; }
        private int Max { get; set; }
        public double StandardDeviation { get; set; }
        public double Mean { get; set; }

        private double _maxToGenerateForProbability;
        private double _minToGenerateForProbability;

        // Key is x, value is p(x)
        private Dictionary<int, double> probabilities = new Dictionary<int, double>();

        public NormalRandomGenerator(int min, int max, double stdev)
        {
            Min = min;
            Max = max;

            // Assume random normal distribution from [min..max]
            // Calculate mean. For [4 .. 6] the mean is 5.
            Mean = ((max - min) / 2) + min;

            // Calculate standard deviation
            int xMinusMyuSquaredSum = 0;
            for (int i = min; i < max; i++)
            {
                xMinusMyuSquaredSum += (int)Math.Pow(i - Mean, 2);
            }

            StandardDeviation = Math.Sqrt(xMinusMyuSquaredSum / (max - min + 1));
            // Flat, uniform distros tend to have a stdev that's too high; for example,
            // for 1-10, stdev is 3, meaning the ranges are 68% in 2-8, and 95% in -1 to 11...
            // So we cut this down to create better statistical variation. We now
            // get numbers like: 1dev=68%, 2dev=95%, 3dev=99% (+= 1%). w00t!
            StandardDeviation *= (0.5);
            StandardDeviation = stdev;
            for (int i = min; i < max; i++)
            {
                probabilities[i] = calculatePdf(i);
                // Eg. if we have: 1 (20%), 2 (60%), 3 (20%), we want to see
                // 1 (20), 2 (80), 3 (100)

                // Avoid index out of range exception
                if (i - 1 >= min)
                {
                    probabilities[i] += probabilities[i - 1];
                }
            }

            _minToGenerateForProbability = probabilities.Values.Min();
            _maxToGenerateForProbability = probabilities.Values.Max();
        }

        public double calculatePdf(int x)
        {
            // Formula from Wikipedia: http://en.wikipedia.org/wiki/Normal_distribution
            // f(x) = e ^ [-(x-myu)^2 / 2*sigma^2]
            //        -------------------------
            //         root(2 * pi * sigma^2)

            double negativeXMinusMyuSquared = -(x - Mean) * (x - Mean);
            double variance = StandardDeviation * StandardDeviation;
            double twoSigmaSquared = 2 * variance;
            double twoPiSigmaSquared = Math.PI * twoSigmaSquared;

            double eExponent = negativeXMinusMyuSquared / twoSigmaSquared;
            double top = Math.Pow(Math.E, eExponent);
            double bottom = Math.Sqrt(twoPiSigmaSquared);

            return top / bottom;
        }
        Random _rnd = new Random();
        public int Next()
        {
            // map [0..1] to [minToGenerateForProbability .. maxToGenerateForProbability]
            // If we have a negative (eg. [-50 to 100]), generate [0 to 150] and subtract 50 to get [-50 to 100]
            double pickedProb = _rnd.NextDouble() * (_maxToGenerateForProbability - _minToGenerateForProbability);
            pickedProb -= _minToGenerateForProbability;

            for (int i = Min; i < Max; i++)
            {
                if (pickedProb <= probabilities[i])
                {
                    return i;
                }
            }

            throw new InvalidOperationException("Internal error: your algorithm is flawed, young Jedi.");
        }
    }

}
