using System;
using System.Collections.Generic;

namespace Berlit
{
    /// <summary>
    /// Контракт "бэкенда" мониторинга. Каждый метод возвращает список
    /// устройств (Component) со вложенными датчиками (Sensor). Устройств может быть
    /// сколько угодно: несколько дисков, видеокарт, планок памяти — GUI покажет все.
    ///
    /// Реализация: SystemHardwareProvider (реестр, счётчики производительности,
    /// DXGI, IOCTL дисков, при наличии прав — WMI). Демо-провайдер с фейковыми
    /// данными отключён (см. DemoHardwareProvider.cs).
    /// </summary>
    public interface IHardwareProvider : IDisposable
    {
        /// <summary>Компьютер, ОС, время работы.</summary>
        IList<HwNode> GetSystemNodes();

        /// <summary>Процессор(ы): загрузка по ядрам, частоты.</summary>
        IList<HwNode> GetCpuNodes();

        /// <summary>Оперативная память: загрузка, модули.</summary>
        IList<HwNode> GetMemoryNodes();

        /// <summary>Видеоядра: загрузка, память, частоты.</summary>
        IList<HwNode> GetGpuNodes();

        /// <summary>Диски и тома: модель, занятое место, состояние.</summary>
        IList<HwNode> GetStorageNodes();

        /// <summary>Материнская плата, BIOS, температуры ACPI.</summary>
        IList<HwNode> GetMainboardNodes();

        /// <summary>Откуда взяты данные и что недоступно (одна строка для статус-бара).</summary>
        string Status { get; }

        /// <summary>Короткая сводка для статус-бара: загрузка CPU, память, GPU.</summary>
        string Summary { get; }

        /// <summary>Компактный снимок для оверлея поверх всех окон.</summary>
        HudSnapshot GetHudSnapshot();
    }
}
