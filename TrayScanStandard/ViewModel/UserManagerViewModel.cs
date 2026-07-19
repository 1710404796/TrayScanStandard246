using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Humanizer;
using LinxUniverse.Auth;
using Microsoft.Win32;
using TrayScanStandard.Attritubes;
using TrayScanStandard.Data.Models;
using TrayScanStandard.View.User;

namespace TrayScanStandard.ViewModel
{
    public partial class UserManagerViewModel : ObservableRecipient
    {
        private readonly UserManager<LinxUser> _userManager;

        [ObservableProperty]
        private ObservableCollection<AdvUser> _users = new();

        [ObservableProperty]
        private RoleEnum _selectedRole = RoleEnum.操作员;

        public List<RoleEnum> RoleList { get; } = Enum.GetValues<RoleEnum>().SkipLast(1).ToList();

        public UserManagerViewModel(UserManager<LinxUser> userManager)
        {
            _userManager = userManager;
        }

        [RelayCommand]
        private async Task ImportCsv()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel/CSV (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|All files (*.*)|*.*",
                DefaultExt = ".xlsx"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var rows = Path.GetExtension(dlg.FileName).ToLowerInvariant() switch
                {
                    ".csv" => ParseCsv(await File.ReadAllLinesAsync(dlg.FileName, Encoding.UTF8)),
                    ".xlsx" or ".xls" => ParseExcel(dlg.FileName),
                    _ => throw new NotSupportedException($"不支持的文件格式: {Path.GetExtension(dlg.FileName)}")
                };

                if (rows.Count == 0)
                {
                    MessageBox.Show("文件中没有有效数据", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = await ImportUsers(rows);
                var parts = new List<string>();
                parts.Add($"成功 {result.Success} 条");
                if (result.Skipped > 0)
                    parts.Add($"已跳过 {result.Skipped} 条（用户已存在）");
                if (result.Fail > 0)
                    parts.Add($"失败 {result.Fail} 条");

                var message = $"导入完成: {string.Join(", ", parts)}";
                if (result.Errors.Count > 0)
                {
                    message += $"\n\n前{Math.Min(10, result.Errors.Count)}条错误:\n{string.Join("\n", result.Errors.Take(10))}";
                }
                MessageBox.Show(message, "导入结果", MessageBoxButton.OK,
                    result.Fail > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 解析 CSV，跳过标题行，返回 (用户名, RFID) 列表
        /// </summary>
        private static List<(string UserName, string Rfid)> ParseCsv(string[] lines)
        {
            if (lines.Length == 0)
                return [];

            var dataLines = lines[0].StartsWith("用户名") || lines[0].StartsWith("username", StringComparison.OrdinalIgnoreCase)
                ? lines.Skip(1)
                : lines;

            return dataLines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(','))
                .Where(p => p.Length >= 2)
                .Select(p => (p[0].Trim(), p[1].Trim()))
                .Where(r => !string.IsNullOrWhiteSpace(r.Item1) && !string.IsNullOrWhiteSpace(r.Item2))
                .ToList();
        }

        /// <summary>
        /// 解析 Excel (.xlsx/.xls)，跳过标题行，返回 (用户名, RFID) 列表
        /// </summary>
        private static List<(string UserName, string Rfid)> ParseExcel(string filePath)
        {
            var rows = new List<(string UserName, string Rfid)>();

            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheet(1);
            var usedRows = sheet.RangeUsed()?.RowsUsed();

            if (usedRows == null)
                return rows;

            bool isFirst = true;
            foreach (var row in usedRows)
            {
                if (isFirst)
                {
                    isFirst = false;
                    var firstCell = row.Cell(1).GetString().Trim();
                    if (firstCell == "用户名" || firstCell.Equals("username", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var userName = row.Cell(1).GetString().Trim();
                var rfid = row.Cell(2).GetString().Trim();

                if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(rfid))
                    rows.Add((userName, rfid));
            }

            return rows;
        }

        private async Task<(int Success, int Skipped, int Fail, List<string> Errors)> ImportUsers(
            List<(string UserName, string Rfid)> rows)
        {
            int success = 0;
            int skipped = 0;
            int fail = 0;
            var errors = new List<string>();
            var existingUsers = (await _userManager.GetAllUserAsync()).ToArray();

            foreach (var (userName, rfid) in rows)
            {
                try
                {
                    if (existingUsers.Any(u => u.UserName == userName))
                    {
                        // 将已存在的用户计为跳过
                        skipped++;
                        continue;
                    }

                    var user = new LinxUser { UserName = userName, Password = rfid };
                    await _userManager.CreateAsync(user);
                    await _userManager.AddToRoleAsync(user, SelectedRole.ToString());
                    Users.Add(new AdvUser { LinxUser = user, Role = SelectedRole });
                    success++;
                }
                catch (Exception ex)
                {
                    fail++;
                    errors.Add($"导入失败 [{userName}]: {ex.Message}");
                }
            }

            return (success, skipped, fail, errors);
        }
    }


    public class AdvUser
    {
        public LinxUser LinxUser { get; set; }
        public string RoleName => Utils.Utils.GetRoleName(Role);
        public RoleEnum Role { get; set; }
        public string PassStar => LinxUser.Password.Truncate(5, "****", Truncator.FixedLength);

    }
}