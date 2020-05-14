using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace TextVoicePlayer
{
    public partial class Form1 : Form
    {
        private WaveOutEvent outputDevice;
        private OffsetSampleProvider offsetSample;
        private AudioFileReader audioFile;

        private string voiceFilePath;   //音声ファイルのパス
        private string voiceFileBaseName;   //音声ファイルのファイル名（拡張子を除く）
        private string voiceFileDirectory;  //音声ファイルのファイルディレクトリ
        private int bytePerSec; //一秒あたりのバイト数
        private int length;     //曲の長さ（秒）
        private int position;   //再生位置（秒）
        private long currentPosition = 0; // 現在の再生位置を記録
        private int nMax = 0;
        string[] startTime;
        string[] pauseTime;


        public Form1()
        {
            InitializeComponent();
        }
        // 音声フィアルを開く

        private void openAudioFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // フォルダーMYMusicにある音声ファイルを開く
            openVoiceFileDialog.InitialDirectory
                = System.Environment.GetFolderPath(Environment.SpecialFolder.MyMusic); //openFileDialog1がイニシャルでMyMusicを開くようにデフォルトで設定
            openVoiceFileDialog.Filter
                = "Voice_file(*.mp3;*.wav)|*.mp3;*.wav|MPEG_Layer-3_file(*.mp3)|*.mp3|wave_file(*.wav" + ")|*.wav"; //音声ファイルのフィルタ

            if (openVoiceFileDialog.ShowDialog() == DialogResult.OK)
            {
                if (audioFile != null)//再生中に音声ファイルを選択できてしまい、多重再生できてしまうための対策。再生中に新しいファイルを選んだときは、再生を停止し、デバイスとファイルをdisposeする。（初期設定）
                {
                    outputDevice.Stop();
                    outputDevice.Dispose();
                    outputDevice = null;
                    audioFile.Dispose();
                    audioFile = null;
                }

                voiceFilePath = openVoiceFileDialog.FileName;

                voiceFileBaseName = Path.GetFileNameWithoutExtension(voiceFilePath);
                voiceFileDirectory = Path.GetDirectoryName(voiceFilePath);

                // Label1のファイル名の表示の処理：ファイル名が長すぎるときに、label1の表示部分が赤くなるのを防ぐ処理(拡張子を含むFileNameが２４文字以上の時、ファイル名のベース部分を切り出し、１０文字目に~~記号を挿入し、ベース部分の後ろから１０を加え、置き換える
                string fileNameLabel1 = Path.GetFileName(voiceFilePath);
                if (fileNameLabel1.Length > 24)
                {
                    fileNameLabel1 = fileNameLabel1.Substring(0, 10)
                        + "~~" + fileNameLabel1.Substring(fileNameLabel1.IndexOf(".") - 10, 10)
                        + Path.GetExtension(voiceFilePath);
                }
                label1.Text = fileNameLabel1;

                currentPosition = 0;

                outputDevice = new WaveOutEvent();
                audioFile = new AudioFileReader(voiceFilePath);
                audioFile.Position = currentPosition;
                outputDevice.Init(audioFile);
                playButton.Image = Properties.Resources.play100_100;

                //必要な値を求める
                bytePerSec = audioFile.WaveFormat.BitsPerSample / 8 * audioFile.WaveFormat.SampleRate * audioFile.WaveFormat.Channels;

                length = (int)audioFile.Length / bytePerSec;
            }

            label3.Text = new TimeSpan(0, 0, length).ToString(); //音源の長さ（時間）を表示


            // STSファイルを開く準備
            openSTSFileDialog.FileName = voiceFileDirectory + "\\" + voiceFileBaseName + ".sts";
            openSTSFile(openSTSFileDialog);     // stsファイルを開く
        }


        // テキストファイルを開く
        private void openSentenceToolStripMenuItem_Click(object sender, EventArgs e)
        {


            openSTSFileDialog.InitialDirectory = voiceFileDirectory; // Audioファイルと同じフォルダを開く

            openSTSFileDialog.Filter = "sts files (*.sts)|*.sts|all files(*.*)|*.*";
            openSTSFileDialog.FileName = voiceFileBaseName;


            if (openSTSFileDialog.ShowDialog() == DialogResult.OK)
            {
                openSTSFile(openSTSFileDialog);     // stsファイルを開く
            }
        }


        // stsファイルを開く（本体）
        private void openSTSFile(OpenFileDialog openSTSFileDialog)
        {
            string stsFilePath;//STSファイルのパス

            stsFilePath = openSTSFileDialog.FileName;

            string stsFileName = Path.GetFileName(stsFilePath);
            if (stsFileName.Length > 24)
            {
                stsFileName = stsFileName.Substring(0, 10)
                    + "~~" + stsFileName.Substring(stsFileName.IndexOf(".") - 10, 10)
                    + Path.GetExtension(voiceFilePath);
            }
            label2.Text = stsFileName;
            //Read the contents of the file into a stream
            try
            {
                var fileStream = openSTSFileDialog.OpenFile();

                using (StreamReader reader = new StreamReader(fileStream))
                {
                    stsText = reader.ReadToEnd();
                }

                stsText = stsText.Replace("\r\n", "\n");
                separateTimeStamp();
            }
            catch (FileNotFoundException)
            {
                return;
            }

        }

        string stsText;

        // 再生ボタンを押したときの処理
        private void playButton_Click(object sender, EventArgs e)
        {
            // 選曲なしでプレーボタンを押したときの処理
            if (audioFile != null) playState();
        }

        bool iPlayAll = false;
        // 再生の状態により、再生開始・一時停止をトグルに切り替える。
        private void playState()
        {
            Console.WriteLine("PlayState={0}   position={1}", outputDevice.PlaybackState, audioFile.Position);
            // 選曲後の処理
            switch (outputDevice.PlaybackState)
            {
                case PlaybackState.Stopped://ファイルが読み込まれてまだ一度も再生されていない場合

                    label3.Text = new TimeSpan(0, 0, length).ToString(); //音源の長さ（時間）を表示

                    if (currentPosition >= audioFile.Length) currentPosition = 0;
                    // 現在の位置を代入
                    audioFile.Position = currentPosition;

                    timer1.Start();

                    outputDevice.Init(audioFile);
                    outputDevice.Play();
                    iPlayAll = true;
                    playButton.Image = Properties.Resources.pause;
                    break;

                case PlaybackState.Paused://一時停止時の場合
                    outputDevice.Dispose();
                    audioFile.Position = currentPosition;
                    outputDevice.Init(audioFile);

                    timer1.Start();

                    outputDevice.Play();
                    iPlayAll = true;

                    playButton.Image = Properties.Resources.pause;
                    break;

                case PlaybackState.Playing://再生中の場合
                    outputDevice.Pause();
                    currentPosition = audioFile.Position;
                    iPlayAll = false;
                    playButton.Image = Properties.Resources.play100_100;

                    break;
            }
        }


        int currentSentenceNumber = 0;
        private void textBox1_Click(object sender, EventArgs e)
        {

            if (audioFile == null) return;

            int caretPosition = richTextBox1.SelectionStart;

            Console.WriteLine("キャレットの位置:" + caretPosition);

            currentSentenceNumber = 0;
            int totalLetterNumber = caretPosition;   // 文頭からキャレットまでの文字の総数
            foreach (string sentenceElement in sentences)
            {
                // 文頭からキャレットまでの全文字数から、各センテンスの文字数を引き、キャレットが何番目のセンテンスか調べる。
                totalLetterNumber = totalLetterNumber - sentenceElement.Length;
                if (totalLetterNumber < 0)
                {
                    break;
                }
                ++currentSentenceNumber;
            }
            playSentence(currentSentenceNumber);
        }


        private void playSentence(int n)
        {
            if (audioFile == null) return;
            if (n <= 0 || n > nMax - 1) return;

            outputDevice.Pause();
            iPlayAll = false;

            timer1.Stop();

            outputDevice.Dispose();

            // 前回、赤色に変えた文字を黒に戻す
            richTextBox1.Select(previousTotalLength - previousSentenceLength, previousSentenceLength);
            richTextBox1.SelectionColor = Color.Black;

            int startToPauseTimeMS = pauseTimeMS[n] - startTimeMS[n];
            if (startToPauseTimeMS <= 0)
            {
                MessageBox.Show("このセンテンスのタイムタグは不正です。");
                return;
            }


            int totalLength = 0;    // 文頭から再生するセンテンスまでのトータルの文字数を入れる変数

            for (int m = 0; m < n + 1; m++)
            {
                totalLength = totalLength + sentences[m].Length;

            }
            // 再生箇所を赤色に変える
            richTextBox1.Select(totalLength - sentences[n].Length, sentences[n].Length);
            richTextBox1.SelectionColor = Color.Red;
            richTextBox1.Select(totalLength, 0);

            previousTotalLength = totalLength;
            previousSentenceLength = sentences[n].Length;


            audioFile.CurrentTime = TimeSpan.FromSeconds(startTimeMS[n] / 1000.0);

            offsetSample = new OffsetSampleProvider(audioFile);
            offsetSample.Take = TimeSpan.FromSeconds(startToPauseTimeMS / 1000.0);

            outputDevice.Init(offsetSample);
            timer1.Start();

            outputDevice.Play();
            playButton.Image = Properties.Resources.pause;

        }



        int mCurrent = -1;      // 再生中のセンテンスを示す添え字を表す変数

        int previousTotalLength = 0;    // 文頭からひとつ前のセンテンスの再生時の再生部分までの文字数（赤文字を黒にするため）
        int previousSentenceLength = 0; // 文頭からひとつ前のセンテンスの再生時のそのセンテンスの文字数（赤文字を黒にするため）

        private void timer1_Tick(object sender, EventArgs e)
        {

            //再生位置（秒）を計算して表示
            currentPosition = audioFile.Position;

            position = (int)currentPosition / bytePerSec;
            label4.Text = new TimeSpan(0, 0, position).ToString();

            // 再生しているセンテンスの色を赤色で強調するための処理
            double currentTime = (double)currentPosition / bytePerSec;
            int totalLength = 0;    // 文頭から再生するセンテンスまでのトータルの文字数を入れる変数

            if (iPlayAll)
            {
                for (int m = 0; m < nMax; m++)
                {
                    totalLength = totalLength + sentences[m].Length;

                    if (currentTime > startTimeMS[m] / 1000 && currentTime < pauseTimeMS[m] / 1000)
                    {
                        if (m != mCurrent)
                        {
                            // 前回、赤色に変えた文字を黒に戻す
                            richTextBox1.Select(previousTotalLength - previousSentenceLength, previousSentenceLength);
                            richTextBox1.SelectionColor = Color.Black;

                            // 再生中の文字を赤に変更し強調する
                            richTextBox1.Select(totalLength - sentences[m].Length, sentences[m].Length);
                            richTextBox1.SelectionColor = Color.Red;
                            richTextBox1.Select(totalLength, 0);

                            mCurrent = m;   // 再生中のセンテンスの番号をmCurrentに記憶し、何度も色変えの処理をしないようにする。

                            // 次のセンテンスを再生するときに、赤文字を黒文字に戻すためにその場所を記憶する。
                            previousTotalLength = totalLength;
                            previousSentenceLength = sentences[m].Length;
                        }
                        break;
                    }
                }
            }

            //再生位置が終了の位置になったときに、停止状態にする
            if (outputDevice.PlaybackState == PlaybackState.Stopped)
            {
                Console.WriteLine("PlaybackState.Stopped");
                playButton.Image = Properties.Resources.play100_100;
                audioFile.Position = 0;
                timer1.Stop();
            }

            // Pauseの時
            if (outputDevice.PlaybackState == PlaybackState.Paused)
            {
                Console.WriteLine("PlaybackState.Paused");

                playButton.Image = Properties.Resources.play100_100;
                timer1.Stop();
            }
        }

        string[] sentences;     // センテンスの配列
        int[] startTimeMS;      // 各センテンスのスタートタイム
        int[] pauseTimeMS;      // 各センテンスのポーズタイム

        private void separateTimeStamp()
        {
            // タイムスタンプの書式（正規表現用）
            var patternStart = @"\[\d{2}:\d{2}.\d{2}\]";
            var patternPause = @"\[/\d{2}:\d{2}.\d{2}\]";

            // STSテキストの中に含まれるタイムスタンプを全て抽出する
            var timeStampsStart = Regex.Matches(stsText, patternStart);
            var timeStampsPause = Regex.Matches(stsText, patternPause);

            // STSテキストの中に含まれるPauseのタイムスタンプを全て除去する。
            foreach (Match match in timeStampsPause)
            {
                stsText = Regex.Replace(stsText, patternPause, "");
            }

            nMax = timeStampsStart.Count + 1;

            startTime = new string[nMax];  // [xx:xx.xx]形式のStartタイムスタンプの配列
            sentences = new string[nMax];  // センテンスの配列
            pauseTime = new string[nMax];  // [xx:xx.xx]形式のPauseタイムスタンプの配列

            startTimeMS = new int[nMax];      // 各センテンスのスタートタイムの配列
            pauseTimeMS = new int[nMax];      // 各センテンスのポーズタイムの配列
            startTimeMS[0] = 0;
            pauseTimeMS[0] = 0;

            sentences[0] = stsText;

            //richTextBox1.Text = "";
            richTextBox1.Clear();


            string text = "";

            //STSテキストの中に含まれるStartのタイムスタンプを全て除去する。 
            for (int n = 0; n < nMax - 1; n++)
            {
                startTime[n + 1] = timeStampsStart[n].Value;

                string[] del = { timeStampsStart[n].Value };
                string[] arr = sentences[n].Split(del, StringSplitOptions.None);
                sentences[n] = arr[0];
                sentences[n + 1] = arr[1];
                Console.WriteLine("sentence[{0}]:{1}]", n, sentences[n]);
                text = text + sentences[n];

                pauseTime[n + 1] = timeStampsPause[n].Value;

                startTimeMS[n + 1] = timeStampToInt(startTime[n + 1]) - 0;
                pauseTimeMS[n + 1] = timeStampToInt(pauseTime[n + 1]) + 0;

                if (n == nMax - 2) text = text + sentences[n + 1];


            }

            richTextBox1.Text = text;
            richTextBox1.SelectAll();
            richTextBox1.SelectionFont = new Font("SimSun", 24, FontStyle.Regular);
            richTextBox1.Select(0, 0);
        }

        // タイムスタンプを整数型（ミリ秒）に変換
        private int timeStampToInt(string timeStamp)
        {
            if (timeStamp == null) return 0;

            // 不要な記号を削除
            string a = timeStamp;
            a = a.Replace("[", "");
            a = a.Replace("/", "");
            a = a.Replace("]", "");

            // 時間形式をミリ秒に変換
            string[] b;
            b = a.Split(':');
            int c = int.Parse(b[0]) * 60000;
            int d = (int)(double.Parse(b[1]) * 1000);
            int intTime = c + d;

            return intTime;
        }

        private void textBox1_DoubleClick(object sender, EventArgs e)
        {
            if (audioFile == null) return;

            outputDevice.Pause();
            iPlayAll = false;

            currentPosition = audioFile.Position;
            playButton.Image = Properties.Resources.play100_100;

        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            currentSentenceNumber++;

            if (currentSentenceNumber > nMax - 1) currentSentenceNumber = nMax - 1;
            playSentence(currentSentenceNumber);
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            currentSentenceNumber--;
            if (currentSentenceNumber < 1) currentSentenceNumber = 1;
            playSentence(currentSentenceNumber);

        }

        private void fontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fontDialog1.Font = richTextBox1.Font;

            if (DialogResult.OK == fontDialog1.ShowDialog())
            {
                richTextBox1.SelectAll();
                richTextBox1.SelectionFont = fontDialog1.Font;
                richTextBox1.Select(0, 0);
            }
        }


        private void allFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.MyMusic); // Audioファイルと同じフォルダを開く

            openFileDialog1.Filter = "text files (*.txt;*.*)|*.txt;*.*";


            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                // stsファイルを開く（本体）
                //Read the contents of the file into a stream
                try
                {
                    var fileStream = openFileDialog1.OpenFile();

                    using (StreamReader reader = new StreamReader(fileStream))
                    {
                        richTextBox1.Text = reader.ReadToEnd();

                    }

                }
                catch (FileNotFoundException)
                {
                    return;
                }


            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Text Voice Player version 0.12\n" +
                "https://github.com/lets-study-with-textvoice \n" +
     "\n" +
     "Special Thanks NAudio\n" +
     "\n" +
     "=========================================\n" +
     "This application may use the NAudio component.\n" +
     "NAudio Project is licensed under the Microsoft Public License (Ms-PL) \n" +
     "https://github.com/naudio\n" +
     "=========================================\n" +
     "\n" +
     "",
     "This application wrote by guijiu.  13.May.2020");

        }

        private void showHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("The help under construction now.");

        }

        private void lisenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form2 form2 = new Form2();
            form2.Show();

        }

        private void escapeClauseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form3 form3 = new Form3();
            form3.Show();

        }
    }
}

