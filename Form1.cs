using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace bag_map
{
    public partial class Form1 : Form
    {
        private readonly Panel sidePanel = new Panel();
        private readonly Panel mainPanel = new Panel();
        private readonly Button[] mapButtons;
        private string currentSelectedMap = string.Empty;
        private PictureBox pictureBox = null!;
        private float zoomFactor = 1.0f;
        private Point dragStartPoint;
        private readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
        private readonly string[] mapNames = { "������", "�̶�", "���̰�", "������", "���˵�", "�е�", "�Ķ��" };
        private readonly string[] buttonTypes = { "����", "��Ʈ�� 1", "��Ʈ�� 2" };
        private const int INITIAL_SIZE = 1280;

        private Label? titleLabel;
        private bool isDetailMode = false;
        private readonly string configPath = Path.Combine(Application.StartupPath, "custom_maps.txt");
        private readonly Dictionary<string, string> customImagePaths = new Dictionary<string, string>();

        public Form1()
        {
            mapButtons = new Button[mapNames.Length];
            InitializeComponent();
            this.FormClosing += (s, e) => CleanupResources();
            SetupForm();
            InitializeUI();
            LoadCustomImagePaths();
            PreloadImages();
        }

        private void LoadCustomImagePaths()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    customImagePaths.Clear();
                    foreach (string line in File.ReadAllLines(configPath).Where(l => !string.IsNullOrWhiteSpace(l)))
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length == 2)
                            customImagePaths[parts[0]] = parts[1];
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"���� ���� �ε� �� ����: {ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SaveCustomImagePaths()
        {
            try
            {
                File.WriteAllLines(configPath, customImagePaths.Select(kvp => $"{kvp.Key}|{kvp.Value}"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"���� ���� ���� �� ����: {ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetupForm()
        {
            this.Text = "��Ʋ�׶��� �� ����";
            this.Size = new Size(1400, 900);
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
        }

        private void InitializeUI()
        {
            // ���̵� �г� ����
            sidePanel.Dock = DockStyle.Left;
            sidePanel.Width = 200;
            sidePanel.BackColor = Color.FromArgb(28, 28, 28);

            // ���� �г� ����
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.MinimumSize = new Size(800, 800);
            mainPanel.BackColor = Color.FromArgb(18, 18, 18);

            // �� ��ư ����
            for (int i = 0; i < mapNames.Length; i++)
            {
                mapButtons[i] = CreateMapButton(mapNames[i], i);
                sidePanel.Controls.Add(mapButtons[i]);
            }

            // ȯ�� ��
            var welcomeLabel = new Label
            {
                Text = "���ʿ��� ���� �������ּ���",
                Font = new Font("���� ���", 20, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true
            };

            // ������ ��
            sidePanel.Controls.Add(new Label
            {
                Text = "by �Ѹ�",
                Font = new Font("���� ���", 14, FontStyle.Bold),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(50, 380 + (mapNames.Length * 60) + 10)
            });

            mainPanel.Controls.Add(welcomeLabel);
            Controls.AddRange(new Control[] { mainPanel, sidePanel });

            // PictureBox ����
            pictureBox = new PictureBox
            {
                Dock = DockStyle.None,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = mainPanel.BackColor,
                Width = INITIAL_SIZE,
                Height = INITIAL_SIZE
            };

            pictureBox.MouseWheel += PictureBox_MouseWheel;
            pictureBox.MouseDown += PictureBox_MouseDown;
            pictureBox.MouseMove += PictureBox_MouseMove;
            pictureBox.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) pictureBox.Cursor = Cursors.Default; };

            mainPanel.Controls.Add(pictureBox);
            mainPanel.PerformLayout();

            // ȯ�� �� �߾� ����
            welcomeLabel.Location = new Point(
                (mainPanel.Width - welcomeLabel.Width) / 2,
                (mainPanel.Height - welcomeLabel.Height) / 2
            );
        }

        private void PreloadImages()
        {
            foreach (var mapName in mapNames)
            {
                foreach (var bType in buttonTypes)
                {
                    string key = $"{mapName}_{bType}";

                    if (customImagePaths.ContainsKey(key) && File.Exists(customImagePaths[key]))
                    {
                        try
                        {
                            imageCache[key] = Image.FromFile(customImagePaths[key]);
                            continue;
                        }
                        catch { }
                    }

                    Image? mapImage = GetMapResourceImage(mapName, bType);
                    if (mapImage != null)
                        imageCache[key] = mapImage;
                }
            }
        }

        private Image? GetMapResourceImage(string mapName, string buttonType)
        {
            try
            {
                switch (mapName)
                {
                    case "������":
                        switch (buttonType)
                        {
                            case "����": return bag_map_image.Resource1.������;
                            case "��Ʈ�� 1": return bag_map_image.Resource1.������_��Ʈ��;
                            case "��Ʈ�� 2": return bag_map_image.Resource1.������_��Ʈ��2;
                        }
                        break;
                    case "�̶�":
                        switch (buttonType)
                        {
                            case "����": return bag_map_image.Resource1.�̶�;
                            case "��Ʈ�� 1": return bag_map_image.Resource1.�̶�_��Ʈ��;
                            case "��Ʈ�� 2": return bag_map_image.Resource1.�̶�_��Ʈ��2;
                        }
                        break;
                    case "���̰�":
                        switch (buttonType)
                        {
                            case "����": return bag_map_image.Resource1.���̰�;
                            case "��Ʈ�� 1": return bag_map_image.Resource1.���̰�_��Ʈ��;
                            case "��Ʈ�� 2": return bag_map_image.Resource1.���̰�_��Ʈ��2;
                        }
                        break;
                    case "������":
                        switch (buttonType)
                        {
                            case "����": return bag_map_image.Resource1.������;
                            case "��Ʈ�� 1": return bag_map_image.Resource1.������_��Ʈ��;
                            case "��Ʈ�� 2": return bag_map_image.Resource1.������_��Ʈ��2;
                        }
                        break;
                    case "���˵�":
                        switch (buttonType)
                        {
                            case "����": return bag_map_image.Resource1.���˵�;
                            case "��Ʈ�� 1": return bag_map_image.Resource1.���˵�_��Ʈ��;
                            case "��Ʈ�� 2": return bag_map_image.Resource1.���˵�_��Ʈ��2;
                        }
                        break;
                    case "�е�":
                        switch (buttonType)
                        {
                            case "����": return bag_map_image.Resource1.�е�;
                            case "��Ʈ�� 1": return bag_map_image.Resource1.�е�_��Ʈ��;
                            case "��Ʈ�� 2": return bag_map_image.Resource1.�е�_��Ʈ��2;
                        }
                        break;
                    case "�Ķ��":
                        if (buttonType == "����")
                            return bag_map_image.Resource1.�Ķ��;
                        break;
                }
            }
            catch { }

            return null;
        }

        private Button CreateMapButton(string mapName, int index)
        {
            var btn = CreateStyledButton(mapName, 180, 50, new Point(10, 20 + (index * 60)));
            btn.Click += (s, e) => {
                currentSelectedMap = mapName;
                isDetailMode = false;
                RemoveChangeUI();
                ShowMapInfo(mapName);
            };
            return btn;
        }

        private Button CreateStyledButton(string text, int width, int height, Point location)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                Location = location,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                Font = new Font("���� ���", 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 45, 45);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 55, 55);

            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(45, 45, 45);
            btn.MouseLeave += (s, e) => btn.BackColor = Color.FromArgb(35, 35, 35);

            return btn;
        }

        private void RemoveChangeUI()
        {
            var toRemove = sidePanel.Controls.Cast<Control>()
                .Where(c => c.Name.StartsWith("change_")).ToList();
            toRemove.ForEach(c => { sidePanel.Controls.Remove(c); c.Dispose(); });
        }

        private void ShowMapInfo(string mapName)
        {
            mainPanel.Controls.Clear();
            mainPanel.Controls.Add(pictureBox);
            isDetailMode = false;
            pictureBox.Visible = false;

            // �� �̸� ��
            titleLabel = new Label
            {
                Text = mapName,
                Font = new Font("���� ���", 24, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true
            };
            mainPanel.Controls.Add(titleLabel);
            mainPanel.PerformLayout();
            titleLabel.Location = new Point((mainPanel.Width - titleLabel.Width) / 2, 50);

            // ��ư ǥ��
            var buttonsToShow = mapName == "�Ķ��" ? new[] { "����" } : buttonTypes;
            int totalWidth = buttonsToShow.Length * 150 + (buttonsToShow.Length - 1) * 20;
            int startX = (mainPanel.Width - totalWidth) / 2;

            for (int i = 0; i < buttonsToShow.Length; i++)
            {
                var typeBtn = CreateStyledButton(buttonsToShow[i], 150, 50,
                    new Point(startX + (i * 170), 400));
                string btnType = buttonsToShow[i];
                typeBtn.Click += (s, e) => ShowMapDetail(mapName, btnType);
                mainPanel.Controls.Add(typeBtn);
            }

            AddMapChangeUIToSidePanel(mapName, buttonsToShow);
            pictureBox.Image = null;
            ResetPictureBoxPosition();
        }

        private void AddMapChangeUIToSidePanel(string mapName, string[] buttonsToShow)
        {
            RemoveChangeUI();
            int startY = 20 + (mapNames.Length * 60);

            sidePanel.Controls.Add(new Label
            {
                Text = "��������������������������������",
                Font = new Font("���� ���", 8),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(10, startY),
                Name = "change_divider"
            });

            for (int i = 0; i < buttonsToShow.Length; i++)
            {
                string buttonType = buttonsToShow[i];

                sidePanel.Controls.Add(new Label
                {
                    Text = buttonType,
                    Font = new Font("���� ���", 9),
                    ForeColor = Color.White,
                    AutoSize = true,
                    Location = new Point(15, startY + 20 + (i * 30)),
                    Name = $"change_label_{i}"
                });

                var changeBtn = new Button
                {
                    Text = "����",
                    Width = 50,
                    Height = 22,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(55, 55, 55),
                    ForeColor = Color.LightGray,
                    Font = new Font("���� ���", 8),
                    Cursor = Cursors.Hand,
                    Location = new Point(130, startY + 20 + (i * 30)),
                    Name = $"change_btn_{i}"
                };

                changeBtn.FlatAppearance.BorderSize = 0;
                changeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 65, 65);
                changeBtn.Click += (s, e) => ChangeMapImage(mapName, buttonType);
                sidePanel.Controls.Add(changeBtn);
            }
        }

        private void ShowMapDetail(string mapName, string buttonType)
        {
            try
            {
                ResetPictureBoxPosition();
                LoadMapImage(mapName, buttonType);

                if (pictureBox.Image != null)
                {
                    pictureBox.Visible = true;
                    mainPanel.Focus();
                    if (!isDetailMode)
                    {
                        isDetailMode = true;

                        if (titleLabel != null)
                        {
                            mainPanel.Controls.Remove(titleLabel);
                            titleLabel.Dispose();
                            titleLabel = null;
                        }

                        // �߾� ��ư ����
                        mainPanel.Controls.Cast<Control>()
                            .Where(c => c is Button && buttonTypes.Contains(c.Text))
                            .ToList()
                            .ForEach(c => { mainPanel.Controls.Remove(c); c.Dispose(); });

                        // ���� �ϴ� ��ư ǥ��
                        ShowMapDetailButtonsLeftBottom();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"�� ������ ǥ�� ����: {ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowMapDetailButtonsLeftBottom()
        {
            var buttonsToShow = currentSelectedMap == "�Ķ��" ? new[] { "����" } : buttonTypes;
            int startY = mainPanel.Height - 200;

            for (int i = 0; i < buttonsToShow.Length; i++)
            {
                string buttonType = buttonsToShow[i];
                var btn = CreateStyledButton(buttonType, 150, 50, new Point(20, startY + (i * 60)));
                btn.Name = $"detail_{buttonType}";
                btn.Click += (s, e) => ShowMapDetail(currentSelectedMap, buttonType);
                mainPanel.Controls.Add(btn);
            }
        }

        private void ChangeMapImage(string mapName, string buttonType)
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "�̹��� ����|*.jpg;*.jpeg;*.png;*.bmp;*.gif|��� ����|*.*",
                Title = $"{mapName} - {buttonType} �̹��� ����"
            })
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string key = $"{mapName}_{buttonType}";
                        imageCache[key]?.Dispose();
                        imageCache[key] = Image.FromFile(dialog.FileName);
                        customImagePaths[key] = dialog.FileName;
                        SaveCustomImagePaths();

                        if (pictureBox.Image != null)
                            ShowMapDetail(mapName, buttonType);

                        MessageBox.Show("�̹����� ���������� ����Ǿ����ϴ�!", "�Ϸ�",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"�̹��� ���� ����: {ex.Message}", "����",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void LoadMapImage(string mapName, string buttonType)
        {
            string key = $"{mapName}_{buttonType}";
            if (imageCache.TryGetValue(key, out Image? value) && value != null)
                pictureBox.Image = value;
            else
                throw new ArgumentException("�̹����� ã�� �� �����ϴ�.");
        }

        private void ResetPictureBoxPosition()
        {
            zoomFactor = 1.0f;
            pictureBox.Size = new Size(INITIAL_SIZE, INITIAL_SIZE);
            pictureBox.Location = new Point(
                (mainPanel.Width - INITIAL_SIZE) / 2,
                (mainPanel.Height - INITIAL_SIZE) / 2
            );
        }

        private void PictureBox_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (sender == null) return;

            float oldZoom = zoomFactor;

            if (e.Delta > 0)
                zoomFactor += 0.1f;
            else if (e.Delta < 0 && zoomFactor > 1.0f)
                zoomFactor = Math.Max(1.0f, zoomFactor - 0.1f);
            else
                return;

            Point mousePos = pictureBox.PointToClient(Cursor.Position);
            float relativeX = mousePos.X / (float)pictureBox.Width;
            float relativeY = mousePos.Y / (float)pictureBox.Height;

            int newSize = (int)(INITIAL_SIZE * zoomFactor);
            int deltaSize = newSize - pictureBox.Width;

            int newX = Math.Max(mainPanel.Width - newSize, Math.Min(0,
                pictureBox.Left - (int)(deltaSize * relativeX)));
            int newY = Math.Max(mainPanel.Height - newSize, Math.Min(0,
                pictureBox.Top - (int)(deltaSize * relativeY)));

            pictureBox.Size = new Size(newSize, newSize);
            pictureBox.Location = new Point(newX, newY);
        }

        private void PictureBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragStartPoint = e.Location;
                pictureBox.Cursor = Cursors.Hand;
            }
        }

        private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int newLeft = Math.Max(mainPanel.Width - pictureBox.Width,
                    Math.Min(0, pictureBox.Left + (e.X - dragStartPoint.X)));
                int newTop = Math.Max(mainPanel.Height - pictureBox.Height,
                    Math.Min(0, pictureBox.Top + (e.Y - dragStartPoint.Y)));

                pictureBox.Location = new Point(newLeft, newTop);
            }
        }

        private void CleanupResources()
        {
            if (pictureBox.Image != null)
            {
                pictureBox.Image = null;
            }

            foreach (var image in imageCache.Values)
                image?.Dispose();
            imageCache.Clear();
        }
    }
}