<p align="center">
  <img src="docs/logo.png" width="132" alt="Berlit logo">
</p>

# Berlit

Монитор оборудования для Windows: процессор, память, видеокарта, накопители,
материнская плата, службы и процессы. Тёмный интерфейс в стиле HWMonitor,
без «демо»-данных — только реальные показания датчиков.

![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6?logo=windows)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-512BD4)
![C%23](https://img.shields.io/badge/C%23-WinForms-239120)

## Возможности

- **Одна строка — одна сущность.** Все параметры ядра (загрузка, температура
  min/сейчас/max, частота, напряжение), видеокарты (загрузка, температура,
  вентилятор, частота, напряжение, мощность) или диска (температура) стоят
  в одной строке в своих колонках — как в HWMonitor.
- **SYSTEM INFO**: статические сведения о компонентах — процессор (как в CPU-Z), видеокарта, модули ОЗУ, плата и BIOS, накопители со SMART. Источники — SMBIOS, DXGI, IOCTL и реестр, без WMI; вкладки CPU / GPU / RAM / MAINBOARD / STORAGE.
- **Оверлей поверх всех окон**: маленькая графитовая плашка с FPS, загрузкой и
  температурами CPU/GPU и занятой памятью. Клики проходят сквозь неё, она не
  перетаскивается и не забирает фокус; включается кнопкой `Overlay` в шапке.
- **Температуры, напряжения, вентиляторы, мощность** через LibreHardwareMonitor:
  температуры ядер CPU и пакета, температуры и напряжения платы (SuperIO),
  вентиляторы, температура и мощность GPU, SMART-температуры дисков.
- **Загрузка CPU** по каждому ядру и эффективная частота — из счётчиков
  производительности Windows.
- **Память**: занято/свободно, модули (тип, объём, частота) — из WMI, если он доступен.
- **Видеокарты**: имя, PCI ID, версия драйвера, LUID, загрузка 3D, занятая
  видеопамять (DXGI + счётчики `GPU Engine`).
- **Накопители**: модель, серийный номер, шина (NVMe/SATA/USB), полный размер,
  тома с занятым местом и файловой системой — через IOCTL, без WMI.
- **Службы и процессы** с сортировкой по колонкам и фильтром «только запущенные».
- Поиск по датчикам и значениям, `Expand all` / `Collapse all`, F5, статус-бар
  с живой сводкой CPU / RAM / GPU.

## Права администратора

Температуры и напряжения читаются через драйвер LibreHardwareMonitor, поэтому
приложение **само запрашивает повышение прав** при запуске (UAC). Это нормальная
практика для аппаратных мониторов.

Если запрос отменить, Berlit запустится в ограниченном режиме: работают загрузка
CPU, память, видеокарта, диски и списки, а датчики температуры/напряжения будут
помечены как `n/a`, в статус-баре появится предупреждение.

Запустить без запроса прав принудительно:

```powershell
Berlit.exe --no-elevate
```

SMART SATA-накопителей и часть датчиков читаются только с правами администратора; NVMe SMART и статические сведения работают и без них.

FPS в оверлее считается по ETW-событиям Present (как в PresentMon) и тоже
требует прав администратора: без них в плашке будет прочерк.

Драйвер `WinRing0`, который использует LibreHardwareMonitorLib 0.9.4, может быть
заблокирован политикой Windows (Memory Integrity / Vulnerable Driver Blocklist)
или антивирусом. В этом случае температуры CPU и платы недоступны даже с правами
администратора — статус-бар сообщит `sensor driver unavailable`.

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
| Память, модули, сокет, кэш | `GlobalMemoryStatusEx`, WMI (если доступен) |
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
