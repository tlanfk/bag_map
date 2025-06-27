using System.Drawing.Drawing2D; // InterpolationMode를 위해 추가

public class FlickerFreePanel : Panel
{
    public FlickerFreePanel()
    {
        this.DoubleBuffered = true;
        this.ResizeRedraw = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x02000000;  // WS_EX_COMPOSITED 플래그 추가
            return cp;
        }
    }
}

namespace bag_map
{
    public partial class Form1 : Form
    {
        private readonly Panel sidePanel = new FlickerFreePanel();
        private readonly Panel mainPanel = new FlickerFreePanel(); // 이미지를 직접 그릴 캔버스
        private readonly Button[] mapButtons;
        private string currentSelectedMap = string.Empty;

        private Image? currentImage;         // 현재 표시 중인 원본 이미지
        private Point imageOffset;           // 패널 내에서 이미지가 그려질 위치 (좌표)
        private Point dragStartPoint;        // 마우스 드래그 시작 위치
        private Point dragStartImageOffset;  // 드래그 시작 시점의 이미지 위치

        // 렌더링 품질 제어를 위한 플래그
        private bool isPanning = false;
        private bool isZooming = false;
        private readonly System.Windows.Forms.Timer zoomFinishTimer; // 줌 동작 완료 감지용 타이머

        private readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
        private readonly string[] mapNames = { "에란겔", "미라마", "태이고", "데스턴", "비켄디", "론도", "파라모" };
        private readonly string[] buttonTypes = { "지도", "히트맵 1", "히트맵 2" };

        // 기본 리소스 이미지 매핑
        private static readonly Dictionary<(string, string), Image> resourceLookup = new()
        {
            { ("에란겔", "지도"), bag_map_image.Resource1.에란겔 },
            { ("에란겔", "히트맵 1"), bag_map_image.Resource1.에란겔_히트맵 },
            { ("에란겔", "히트맵 2"), bag_map_image.Resource1.에란겔_히트맵2 },
            { ("미라마", "지도"), bag_map_image.Resource1.미라마 },
            { ("미라마", "히트맵 1"), bag_map_image.Resource1.미라마_히트맵 },
            { ("미라마", "히트맵 2"), bag_map_image.Resource1.미라마_히트맵2 },
            { ("태이고", "지도"), bag_map_image.Resource1.테이고 },
            { ("태이고", "히트맵 1"), bag_map_image.Resource1.테이고_히트맵 },
            { ("태이고", "히트맵 2"), bag_map_image.Resource1.테이고_히트맵2 },
            { ("데스턴", "지도"), bag_map_image.Resource1.데스턴 },
            { ("데스턴", "히트맵 1"), bag_map_image.Resource1.데스턴_히트맵 },
            { ("데스턴", "히트맵 2"), bag_map_image.Resource1.데스턴_히트맵2 },
            { ("비켄디", "지도"), bag_map_image.Resource1.비켄디 },
            { ("비켄디", "히트맵 1"), bag_map_image.Resource1.비켄디_히트맵 },
            { ("비켄디", "히트맵 2"), bag_map_image.Resource1.비켄디_히트맵2 },
            { ("론도", "지도"), bag_map_image.Resource1.론도 },
            { ("론도", "히트맵 1"), bag_map_image.Resource1.론도_히트맵 },
            { ("론도", "히트맵 2"), bag_map_image.Resource1.론도_히트맵2 },
            { ("파라모", "지도"), bag_map_image.Resource1.파라모 }
        };

        private Label? titleLabel;
        private bool isDetailMode = false;
        private readonly string configPath = Path.Combine(Application.StartupPath, "custom_maps.txt");
        private readonly Dictionary<string, string> customImagePaths = new Dictionary<string, string>();

        // 감도 설정 변수들 (초기값은 LoadSettings에서 덮어쓰일 수 있음)
        private float zoomSensitivity = 0.1f;
        private float panSensitivity = 1.0f;
        private Size originalImageSize;
        private float baseScale = 1f;
        private float userZoom = 1f;
        private const float MAX_ZOOM = 8f;
        private const float MIN_ZOOM = 1.3f; // 최소 축소 한계치

        public Form1()
        {
            mapButtons = new Button[mapNames.Length];

            // 줌 타이머 초기화
            zoomFinishTimer = new System.Windows.Forms.Timer();
            zoomFinishTimer.Interval = 150; // 0.15초 동안 휠 입력이 없으면 줌이 끝난 것으로 간주
            zoomFinishTimer.Tick += ZoomFinishTimer_Tick;

            InitializeComponent();
            this.FormClosing += (s, e) => CleanupResources();
            SetupForm();
            InitializeUI();
            LoadSettings(); // 설정 로드 메서드 호출
            PreloadImages();
        }

        // 감도 설정까지 포함하여 로드하는 메서드
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    customImagePaths.Clear();
                    foreach (string line in File.ReadAllLines(configPath).Where(l => !string.IsNullOrWhiteSpace(l)))
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length != 2) continue;

                        string key = parts[0];
                        string value = parts[1];

                        // 설정 키를 확인하여 감도 값을 로드
                        switch (key)
                        {
                            case "config_zoom_sensitivity":
                                float.TryParse(value, out zoomSensitivity);
                                break;
                            case "config_pan_sensitivity":
                                float.TryParse(value, out panSensitivity);
                                break;
                            default: // 그 외에는 이미지 경로로 취급
                                customImagePaths[key] = value;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 파일 로드 중 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                // 로드된 값으로 UI 업데이트
                UpdateSensitivityControls();
            }
        }

        // 감도 설정까지 포함하여 저장하는 메서드
        private void SaveSettings()
        {
            try
            {
                // 이미지 경로와 설정 값을 모두 포함하여 저장
                var linesToSave = customImagePaths.Select(kvp => $"{kvp.Key}|{kvp.Value}").ToList();
                linesToSave.Add($"config_zoom_sensitivity|{zoomSensitivity}");
                linesToSave.Add($"config_pan_sensitivity|{panSensitivity}");

                File.WriteAllLines(configPath, linesToSave);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 파일 저장 중 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetupForm()
        {
            this.Text = "배틀그라운드 맵 정보";
            this.Size = new Size(1400, 900);
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;
        }

        private void InitializeUI()
        {
            sidePanel.Dock = DockStyle.Left;
            sidePanel.Width = 200;
            sidePanel.BackColor = Color.FromArgb(28, 28, 28);

            mainPanel.Dock = DockStyle.Fill;
            mainPanel.MinimumSize = new Size(800, 800);
            mainPanel.BackColor = Color.FromArgb(18, 18, 18);
            mainPanel.Paint += mainPanel_Paint;
            mainPanel.MouseDown += mainPanel_MouseDown;
            mainPanel.MouseMove += mainPanel_MouseMove;
            mainPanel.MouseUp += mainPanel_MouseUp;
            mainPanel.MouseWheel += mainPanel_MouseWheel;

            for (int i = 0; i < mapNames.Length; i++)
            {
                mapButtons[i] = CreateMapButton(mapNames[i], i);
                sidePanel.Controls.Add(mapButtons[i]);
            }

            AddSensitivityControls();

            var welcomeLabel = new Label
            {
                Text = "왼쪽에서 맵을 선택해주세요",
                Font = new Font("맑은 고딕", 20, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Name = "welcomeLabel"
            };

            sidePanel.Controls.Add(new Label
            {
                Text = "by 둘리",
                Font = new Font("맑은 고딕", 14, FontStyle.Bold),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(50, 800)
            });

            mainPanel.Controls.Add(welcomeLabel);
            Controls.AddRange(new Control[] { mainPanel, sidePanel });

            mainPanel.PerformLayout();
            welcomeLabel.Location = new Point(
                (mainPanel.Width - welcomeLabel.Width) / 2,
                (mainPanel.Height - welcomeLabel.Height) / 2
            );
        }

        private void AddSensitivityControls()
        {
            int startY = 120 + (mapNames.Length * 60) + 20;

            sidePanel.Controls.Add(new Label
            {
                Text = "━━━━ 감도 설정 ━━━━━",
                Font = new Font("맑은 고딕", 9, FontStyle.Bold),
                ForeColor = Color.LightGray,
                AutoSize = true,
                Location = new Point(15, startY - 10),
                Name = "sensitivity_title"
            });

            sidePanel.Controls.Add(new Label
            {
                Text = "확대/축소:",
                Font = new Font("맑은 고딕", 8),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, startY + 20),
                Name = "zoom_label"
            });

            var zoomTrackBar = new TrackBar
            {
                Minimum = 10,
                Maximum = 30,
                Width = 150,
                Height = 30,
                Location = new Point(7, startY + 40),
                Name = "zoom_trackbar"
            };
            zoomTrackBar.ValueChanged += (s, e) =>
            {
                zoomSensitivity = zoomTrackBar.Value / 100.0f;
                UpdateSensitivityLabels();
                SaveSettings();
            };
            sidePanel.Controls.Add(zoomTrackBar);

            sidePanel.Controls.Add(new Label
            {
                Text = "이동 감도:",
                Font = new Font("맑은 고딕", 8),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, startY + 90),
                Name = "pan_label"
            });

            var panTrackBar = new TrackBar
            {
                Minimum = 1,
                Maximum = 20, // 이동 감도 최대치를 2.0으로 변경
                Width = 150,
                Height = 30,
                Location = new Point(7, startY + 110),
                Name = "pan_trackbar"
            };
            panTrackBar.ValueChanged += (s, e) =>
            {
                panSensitivity = panTrackBar.Value / 10.0f;
                UpdateSensitivityLabels();
                SaveSettings();
            };
            sidePanel.Controls.Add(panTrackBar);

            sidePanel.Controls.Add(new Label { Font = new Font("맑은 고딕", 7), ForeColor = Color.Gray, AutoSize = true, Location = new Point(170, startY + 30), Name = "zoom_value_label" });
            sidePanel.Controls.Add(new Label { Font = new Font("맑은 고딕", 7), ForeColor = Color.Gray, AutoSize = true, Location = new Point(170, startY + 90), Name = "pan_value_label" });

            var resetBtn = CreateStyledButton("기본값", 80, 25, new Point(10, startY + 150));
            resetBtn.Font = new Font("맑은 고딕", 8);
            resetBtn.Name = "reset_sensitivity";
            resetBtn.Click += (s, e) =>
            {
                zoomSensitivity = 0.1f;
                panSensitivity = 1.0f;
                UpdateSensitivityControls();
                SaveSettings();
            };
            sidePanel.Controls.Add(resetBtn);
        }

        // 변수 값에 따라 감도 조절 UI를 업데이트하는 메서드
        private void UpdateSensitivityControls()
        {
            var zoomTrack = sidePanel.Controls["zoom_trackbar"] as TrackBar;
            var panTrack = sidePanel.Controls["pan_trackbar"] as TrackBar;

            if (zoomTrack != null) zoomTrack.Value = (int)(zoomSensitivity * 100);
            if (panTrack != null) panTrack.Value = (int)(panSensitivity * 10);

            UpdateSensitivityLabels();
        }

        private void UpdateSensitivityLabels()
        {
            var zoomValueLabel = sidePanel.Controls["zoom_value_label"] as Label;
            var panValueLabel = sidePanel.Controls["pan_value_label"] as Label;

            if (zoomValueLabel != null) zoomValueLabel.Text = $"({zoomSensitivity:F2})";
            if (panValueLabel != null) panValueLabel.Text = $"({panSensitivity:F1})";
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
                        try { imageCache[key] = Image.FromFile(customImagePaths[key]); continue; }
                        catch { }
                    }
                    Image? mapImage = GetMapResourceImage(mapName, bType);
                    if (mapImage != null) imageCache[key] = mapImage;
                }
            }
        }

        private Image? GetMapResourceImage(string mapName, string buttonType)
        {
            if (resourceLookup.TryGetValue((mapName, buttonType), out var img))
            {
                return img;
            }
            return null;
        }

        private Button CreateMapButton(string mapName, int index)
        {
            var btn = CreateStyledButton(mapName, 180, 50, new Point(10, 20 + (index * 60)));
            btn.Click += (s, e) => { currentSelectedMap = mapName; isDetailMode = false; RemoveChangeUI(); ShowMapInfo(mapName); };
            return btn;
        }

        private Button CreateStyledButton(string text, int width, int height, Point location)
        {
            var btn = new Button { Text = text, Width = width, Height = height, Location = location, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(35, 35, 35), ForeColor = Color.White, Font = new Font("맑은 고딕", 12, FontStyle.Bold), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 45, 45);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 55, 55);
            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(45, 45, 45);
            btn.MouseLeave += (s, e) => btn.BackColor = Color.FromArgb(35, 35, 35);
            return btn;
        }

        private void RemoveChangeUI()
        {
            var toRemove = sidePanel.Controls.Cast<Control>().Where(c => c.Name.StartsWith("change_")).ToList();
            toRemove.ForEach(c => { sidePanel.Controls.Remove(c); c.Dispose(); });
        }

        private void ShowMapInfo(string mapName)
        {
            ClearMainPanel();
            isDetailMode = false;
            titleLabel = new Label { Text = mapName, Font = new Font("맑은 고딕", 24, FontStyle.Bold), ForeColor = Color.White, AutoSize = true };
            mainPanel.Controls.Add(titleLabel);
            mainPanel.PerformLayout();
            titleLabel.Location = new Point((mainPanel.Width - titleLabel.Width) / 2, 50);
            var buttonsToShow = mapName == "파라모" ? new[] { "지도" } : buttonTypes;
            int totalWidth = buttonsToShow.Length * 150 + (buttonsToShow.Length - 1) * 20;
            int startX = (mainPanel.Width - totalWidth) / 2;
            for (int i = 0; i < buttonsToShow.Length; i++)
            {
                var typeBtn = CreateStyledButton(buttonsToShow[i], 150, 50, new Point(startX + (i * 170), 400));
                string btnType = buttonsToShow[i];
                typeBtn.Click += (s, e) => ShowMapDetail(mapName, btnType);
                mainPanel.Controls.Add(typeBtn);
            }
            AddMapChangeUIToSidePanel(mapName, buttonsToShow);
            currentImage = null;
            ResetImageState();
            mainPanel.Invalidate();
        }

        private void ClearMainPanel()
        {
            var controlsToRemove = mainPanel.Controls.OfType<Control>().ToList();
            foreach (var ctrl in controlsToRemove) { mainPanel.Controls.Remove(ctrl); ctrl.Dispose(); }
        }

        private void AddMapChangeUIToSidePanel(string mapName, string[] buttonsToShow)
        {
            RemoveChangeUI();
            int startY = 20 + (mapNames.Length * 60);
            sidePanel.Controls.Add(new Label { Text = "━━━━━ 지도 설정 ━━━━━", Font = new Font("맑은 고딕", 8), ForeColor = Color.Gray, AutoSize = true, Location = new Point(15, startY + 3), Name = "change_divider" });
            for (int i = 0; i < buttonsToShow.Length; i++)
            {
                string buttonType = buttonsToShow[i];
                sidePanel.Controls.Add(new Label { Text = buttonType, Font = new Font("맑은 고딕", 9), ForeColor = Color.White, AutoSize = true, Location = new Point(15, startY + 20 + (i * 30)), Name = $"change_label_{i}" });
                var changeBtn = new Button { Text = "변경", Width = 50, Height = 22, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.LightGray, Font = new Font("맑은 고딕", 8), Cursor = Cursors.Hand, Location = new Point(130, startY + 20 + (i * 30)), Name = $"change_btn_{i}" };
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
                LoadMapImage(mapName, buttonType);
                ResetImageState();
                if (currentImage != null)
                {
                    mainPanel.Focus();
                    if (!isDetailMode)
                    {
                        isDetailMode = true;
                        ClearMainPanel();
                        ShowMapDetailButtonsLeftBottom();
                    }
                    else
                    {
                        foreach (Control control in mainPanel.Controls) { if (control is Button btn && btn.Name.StartsWith("detail_")) { btn.BackColor = btn.Text == buttonType ? Color.FromArgb(45, 45, 45) : Color.FromArgb(35, 35, 35); } }
                    }
                    mainPanel.Invalidate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"맵 상세정보 표시 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowMapDetailButtonsLeftBottom()
        {
            var existingDetailButtons = mainPanel.Controls.Cast<Control>().Where(c => c is Button && c.Name.StartsWith("detail_")).ToList();
            existingDetailButtons.ForEach(c => { mainPanel.Controls.Remove(c); c.Dispose(); });
            var buttonsToShow = currentSelectedMap == "파라모" ? new[] { "지도" } : buttonTypes;
            int startY = mainPanel.Height - 200;
            for (int i = 0; i < buttonsToShow.Length; i++)
            {
                string buttonType = buttonsToShow[i];
                var btn = CreateStyledButton(buttonType, 150, 50, new Point(20, startY + (i * 60)));
                btn.Name = $"detail_{buttonType}";
                btn.Click += (s, e) => ShowMapDetail(currentSelectedMap, buttonType);
                mainPanel.Controls.Add(btn);
                btn.BringToFront();
            }
        }

        private void ChangeMapImage(string mapName, string buttonType)
        {
            using (var dialog = new OpenFileDialog { Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp;*.gif|모든 파일|*.*", Title = $"{mapName} - {buttonType} 이미지 선택" })
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string key = $"{mapName}_{buttonType}";
                        imageCache[key]?.Dispose();
                        imageCache[key] = Image.FromFile(dialog.FileName);
                        customImagePaths[key] = dialog.FileName;
                        SaveSettings();
                        if (isDetailMode) ShowMapDetail(mapName, buttonType);
                        MessageBox.Show("이미지가 성공적으로 변경되었습니다!", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"이미지 변경 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        private void LoadMapImage(string mapName, string buttonType)
        {
            // [오류 수정] 'bType'을 올바른 매개변수 'buttonType'으로 수정
            string key = $"{mapName}_{buttonType}";
            if (!imageCache.TryGetValue(key, out Image? img) || img == null) throw new ArgumentException("이미지를 찾을 수 없습니다.");
            currentImage = img;
            originalImageSize = img.Size;
        }

        private void ResetImageState()
        {
            if (currentImage == null) return;
            baseScale = Math.Min(mainPanel.ClientSize.Width / (float)originalImageSize.Width, mainPanel.ClientSize.Height / (float)originalImageSize.Height);
            baseScale = Math.Min(1f, baseScale);
            userZoom = 1.4f;
            ApplyScaleAndCenter();
            mainPanel.Invalidate();
        }

        private void ApplyScaleAndCenter()
        {
            if (currentImage == null) return;
            float currentScale = baseScale * userZoom;
            Size scaledSize = new Size((int)(originalImageSize.Width * currentScale), (int)(originalImageSize.Height * currentScale));
            imageOffset = new Point((mainPanel.ClientSize.Width - scaledSize.Width) / 2, (mainPanel.ClientSize.Height - scaledSize.Height) / 2);
        }

        // [오류 수정] sender의 Null 허용 여부를 'object?'로 변경
        private void mainPanel_Paint(object? sender, PaintEventArgs e)
        {
            if (currentImage == null) return;

            // 줌 또는 패닝 중에는 저화질, 끝나면 고화질로 렌더링
            if (isPanning || isZooming) { e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor; }
            else { e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; }

            float currentScale = baseScale * userZoom;
            var imageTotalBounds = new Rectangle(imageOffset.X, imageOffset.Y, (int)(originalImageSize.Width * currentScale), (int)(originalImageSize.Height * currentScale));
            var panelVisibleBounds = mainPanel.ClientRectangle;
            Rectangle visibleImageRectOnScreen = Rectangle.Intersect(panelVisibleBounds, imageTotalBounds);
            if (visibleImageRectOnScreen.Width > 0 && visibleImageRectOnScreen.Height > 0)
            {
                float srcX = (visibleImageRectOnScreen.X - imageOffset.X) / currentScale;
                float srcY = (visibleImageRectOnScreen.Y - imageOffset.Y) / currentScale;
                float srcWidth = visibleImageRectOnScreen.Width / currentScale;
                float srcHeight = visibleImageRectOnScreen.Height / currentScale;
                RectangleF sourceRect = new RectangleF(srcX, srcY, srcWidth, srcHeight);
                e.Graphics.DrawImage(currentImage, visibleImageRectOnScreen, sourceRect, GraphicsUnit.Pixel);
            }
        }

        private void mainPanel_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (currentImage == null) return;
            isZooming = true; // 줌 시작
            zoomFinishTimer.Stop(); // 연속적인 휠 동작을 위해 타이머 리셋

            Point mousePos = e.Location;
            float oldZoom = userZoom;

            if (e.Delta > 0) userZoom = Math.Min(MAX_ZOOM, userZoom + zoomSensitivity);
            else userZoom = Math.Max(MIN_ZOOM, userZoom - zoomSensitivity);

            if (Math.Abs(oldZoom - userZoom) < 0.001f) { isZooming = false; return; }

            float newScale = baseScale * userZoom;
            float oldScale = baseScale * oldZoom;
            float imageX = (mousePos.X - imageOffset.X) / oldScale;
            float imageY = (mousePos.Y - imageOffset.Y) / oldScale;
            int newOffsetX = (int)(mousePos.X - (imageX * newScale));
            int newOffsetY = (int)(mousePos.Y - (imageY * newScale));
            imageOffset = new Point(newOffsetX, newOffsetY);

            ConstrainImageOffset();
            mainPanel.Invalidate();
            zoomFinishTimer.Start(); // 줌 종료 감지를 위해 타이머 시작
        }

        // 줌 동작이 끝나면 고화질 렌더링을 위해 호출되는 이벤트 핸들러
        private void ZoomFinishTimer_Tick(object? sender, EventArgs e)
        {
            zoomFinishTimer.Stop();
            isZooming = false;
            mainPanel.Invalidate(); // 최종 고화질 렌더링
        }


        private void mainPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && currentImage != null)
            {
                isPanning = true;
                dragStartPoint = e.Location;
                dragStartImageOffset = imageOffset;
                mainPanel.Cursor = Cursors.Hand;
            }
        }

        private void mainPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!isPanning) return;
            int dx = (int)((e.X - dragStartPoint.X) * panSensitivity);
            int dy = (int)((e.Y - dragStartPoint.Y) * panSensitivity);
            imageOffset = new Point(dragStartImageOffset.X + dx, dragStartImageOffset.Y + dy);
            ConstrainImageOffset();
            mainPanel.Invalidate();
        }

        private void mainPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                mainPanel.Cursor = Cursors.Default;
                if (isPanning)
                {
                    isPanning = false;
                    mainPanel.Invalidate();
                }
            }
        }

        private void ConstrainImageOffset()
        {
            if (currentImage == null) return;
            float currentScale = baseScale * userZoom;
            Size scaledSize = new Size((int)(originalImageSize.Width * currentScale), (int)(originalImageSize.Height * currentScale));
            int minX = mainPanel.ClientSize.Width - scaledSize.Width;
            int minY = mainPanel.ClientSize.Height - scaledSize.Height;
            if (scaledSize.Width <= mainPanel.ClientSize.Width) imageOffset.X = (mainPanel.ClientSize.Width - scaledSize.Width) / 2;
            else imageOffset.X = Math.Min(0, Math.Max(minX, imageOffset.X));
            if (scaledSize.Height <= mainPanel.ClientSize.Height) imageOffset.Y = (mainPanel.ClientSize.Height - scaledSize.Height) / 2;
            else imageOffset.Y = Math.Min(0, Math.Max(minY, imageOffset.Y));
        }

        private void CleanupResources()
        {
            currentImage = null;
            foreach (var image in imageCache.Values) image?.Dispose();
            imageCache.Clear();
        }
    }
}
