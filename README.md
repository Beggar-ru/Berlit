<p align="center">
  <img src="docs/logo.png" width="132" alt="Berlit logo">
</p>

# Berlit

![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6?logo=windows)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-512BD4)
![C%23](https://img.shields.io/badge/C%23-WinForms-239120)

## История проекта

Проект начинался как **AdminHelper** — приложение для дипломной работы. Учебный
проект накопил много проблем, а часть функций оказалась лишней и мешала
продукту. Отказавшись от них, я сформировал **Berlit**.

Здесь проблем меньше: приложение строится на уже имеющихся знаниях и точном
понимании того, каким должен получиться продукт.

## Сборка

Требуется Windows 10 x64 или новее, .NET Framework 4.7.2 и MSBuild/Visual Studio.

```powershell
msbuild Berlit\Berlit.csproj -t:Restore,Build -p:Configuration=Release
```

NuGet-пакет `LibreHardwareMonitorLib` восстанавливается автоматически. Готовый
бинарник: `Berlit\bin\Release\Berlit.exe` (x64).

## Источники данных

| Что | Откуда |
|---|---|
| Температуры, напряжения, вентиляторы, мощность | LibreHardwareMonitorLib (драйвер, нужен администратор) |
| Загрузка/частота CPU | счётчики `Processor`, `Processor Information` |
| Загрузка GPU, видеопамять | DXGI + счётчики `GPU Engine`, `GPU Adapter Memory` |
| Температуры ACPI-зон | счётчики `Thermal Zone Information` |
| Диски: модель, серийник, шина, размер | IOCTL `PHYSICALDRIVEn` (`STORAGE_QUERY_PROPERTY`, `DISK_GET_DRIVE_GEOMETRY_EX`) |
| Тома, свободное место | `DriveInfo` |
| Плата, BIOS, ОС, драйвер GPU | реестр |
| Статические сведения (CPU, GPU, модули ОЗУ, плата, диски) | SMBIOS, DXGI, IOCTL, реестр |
| Память, модули, сокет, кэш | `GlobalMemoryStatusEx`, SMBIOS, WMI (если доступен) |
| Службы, процессы | SCM `ServiceController`, `System.Diagnostics` |

Модули WMI опциональны: если служба WMI закрыта, приложение продолжает работать
на реестре, счётчиках и IOCTL.

## Ограничения

- Нет собственного драйвера: всё, что нельзя прочитать через Win32 API, берётся
  у LibreHardwareMonitor.
- SMART-температуры SATA/NVMe, температуры ядер и напряжения платы требуют прав
  администратора.
- На некоторых платах SuperIO не поддерживается — соответствующие датчики просто
  не появятся.

## Сторонние компоненты

- [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) — MPL-2.0
- HidSharp — http://www.zer7.com/software/hidsharp (зависимость LibreHardwareMonitorLib)
- System.CodeDom — MIT, Microsoft

Подробнее — в [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Структура

```
Berlit.slnx                 решение
Berlit/
  Berlit.cs                 окно: тема, обновление, фильтры, сортировки
  Berlit.Designer.cs        раскладка
  SystemHardwareProvider.cs сбор данных обо всём железе
  LibreHardwareSensors.cs   обёртка над LibreHardwareMonitor
  PerfSampler.cs            счётчики CPU/GPU/термозон
  StorageProbe.cs           диски через IOCTL
  DxgiAdapters.cs           перечисление GPU
  MachineInfo.cs            реестр
  WmiProbe.cs               необязательный WMI
  HwTreeGrid.cs             дерево датчиков
  DataTableGrid.cs          таблицы служб/процессов
  Theme.cs                  палитра
```

## Лицензия

Berlit распространяется бесплатно по открытой лицензии с открытым исходным
кодом — [MIT](LICENSE).
