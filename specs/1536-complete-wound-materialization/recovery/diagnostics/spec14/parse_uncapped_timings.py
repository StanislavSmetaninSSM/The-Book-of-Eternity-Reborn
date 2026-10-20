"""Reconstruct saved named-operation measurements from their raw child output.

This offline reader never starts the game or changes its fixtures. Use --check
to verify the existing deterministic JSON/Markdown reports without writing them.
"""

from pathlib import Path
import argparse
from datetime import datetime
import hashlib
import json
import math
import re
import statistics
import subprocess
import xml.etree.ElementTree as ET
import zipfile


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(raw):
    return hashlib.sha256(raw).hexdigest().upper()


def records(raw):
    result = []
    for line in raw.splitlines():
        if not line.startswith("{"):
            continue
        try:
            value = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(value, dict):
            result.append(value)
    return result


def build_report(repo, verify_live=False, report_prefix="T302"):
    evidence = repo / "specs/1536-complete-wound-materialization/recovery/crash5"
    measurement, preparation = {"T302": ("T301", "T300"), "T306": ("T305", "T303"),
                                "T311": ("T310", "T309-seam"), "T316": ("T315", "T314"), "T324": ("T323", "T320")}[report_prefix]
    generation = {"T302": "T301", "T306": "T304", "T311": "T310", "T316": "T315", "T324": "T322"}[report_prefix]
    stem = measurement + "-profile-cut0"
    outcome = load(evidence / (stem + "-outcome.json"))
    archive_path = evidence / (stem + "-raw.zip")
    assert digest(archive_path.read_bytes()) == outcome["ArchiveSHA256"]
    archive_index = []
    with zipfile.ZipFile(archive_path) as archive:
        assert archive.testzip() is None, "Bad ZIP CRC"
        members = archive.namelist()
        assert len(members) == len(set(members)), "Duplicate archive members"
        assert all(not x.startswith(("/", "\\")) and ".." not in x.split("/") for x in members)
        for member in archive.infolist():
            if not member.is_dir():
                raw = archive.read(member)
                archive_index.append({"Path": member.filename, "Bytes": len(raw), "SHA256": digest(raw)})
        assert json.loads(archive.read("summary.json")) == outcome["Summary"]
        assert json.loads(archive.read(generation + "-seed-binding.json")) == outcome["SeedBinding"]
        prep = json.loads(archive.read("T314-prep-v2.json" if report_prefix == "T316" else preparation + "-prep.json"))
        assert (prep["Source"] if report_prefix in ("T311", "T316", "T324") else prep["Binding"]["CompiledSource"]) == outcome["CompiledSource"]
        ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
        results = []
        for member in members:
            if member.endswith(".trx"):
                results.extend(ET.fromstring(archive.read(member)).findall(".//t:Results/t:UnitTestResult", ns))
        assert len(results) == 1
        assert results[0].get("outcome") == outcome["Outcome"]
        assert results[0].get("duration") == outcome["TestDuration"]
        outer = records(results[0].findtext("t:Output/t:StdOut", "", ns))
        children = [x["Observation"] for x in outer if x.get("Boundary") == "iobs_actual_native_join"]
    assert len(children) == len(outcome["Children"])
    stream_bindings = []
    for index, child in enumerate(children):
        assert child["Id"] == outcome["Children"][index]["Id"]
        for stream in ("stdout", "stderr"):
            path = evidence / f"{stem}-child{index}.{stream}"
            raw = path.read_bytes()
            assert raw == (child.get("Raw" + stream.title()) or "").encode("utf-8")
            stream_bindings.append({"Path": path.name, "PID": child["Id"], "Bytes": len(raw), "SHA256": digest(raw)})
    resumers = [x for x in children if "engine-joint-interface-resume" in x["Arguments"]]
    assert len(resumers) == 1, "No unique actual resumer; do not invent timing results"
    resumer = resumers[0]
    assert len(children) == 2 and children[0]["Id"] != resumer["Id"]
    samples = [x for x in records(resumer.get("RawStdout") or "") if x.get("Boundary") == "joint_operation_timing"]
    assert samples, "The actual resumer emitted no named timing records"
    assert not any(x.get("Boundary") == "joint_operation_timing" for x in records(children[0].get("RawStdout") or ""))

    source_prefix = outcome["CompiledSource"] + ":"
    matching = subprocess.check_output([
        "git", "grep", "--name-only", "-F", 'MeasureOperation("', outcome["CompiledSource"], "--",
        "BookOfEternityClient", "BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrash.cs",
    ], cwd=repo, encoding="utf-8").splitlines()
    assert matching and all(x.startswith(source_prefix) for x in matching)
    source_paths = [x[len(source_prefix):] for x in matching]
    locations = {}
    for path in source_paths:
        source_bytes = subprocess.check_output(["git", "show", outcome["CompiledSource"] + ":" + path], cwd=repo)
        for number, line in enumerate(source_bytes.decode("utf-8-sig").splitlines(), 1):
            match = re.search(r'MeasureOperation\("([^"]+)"\)', line)
            if match:
                locations.setdefault(match[1], []).append({"Path": path, "Line": number})

    pending = []
    intervals = []
    last_elapsed = -1.0
    limited = False
    for expected, sample in enumerate(samples, 1):
        assert sample["Sequence"] == expected and sample["PID"] == resumer["Id"]
        elapsed = sample["TotalElapsedMilliseconds"]
        ms = sample["Milliseconds"]
        assert isinstance(elapsed, (float, int)) and not isinstance(elapsed, bool) and math.isfinite(elapsed)
        assert isinstance(ms, (float, int)) and not isinstance(ms, bool) and math.isfinite(ms)
        assert elapsed >= last_elapsed and ms >= 0
        last_elapsed = elapsed
        assert sample["State"] != "limited", "Unexpected diagnostic output cutoff"
        assert sample["Operation"] in locations, "Unknown nonliteral operation name"
        if sample["State"] == "begin":
            assert ms == 0
            sample["ParentBeginSequence"] = pending[-1]["Sequence"] if pending else None
            pending.append(sample)
        else:
            assert sample["State"] == "end" and pending
            begin = pending.pop()
            assert begin["Operation"] == sample["Operation"], "Non-LIFO spans; investigate rather than mispair"
            assert elapsed >= begin["TotalElapsedMilliseconds"]
            # Scope elapsed excludes its own output observer calls. Both clocks
            # are monotonic, but parent wait/cleanup clocks are not this clock.
            assert ms <= elapsed - begin["TotalElapsedMilliseconds"] + 1.0
            intervals.append({
                "Operation": sample["Operation"], "BeginSequence": begin["Sequence"],
                "EndSequence": sample["Sequence"], "Milliseconds": ms, "ParentBeginSequence": begin["ParentBeginSequence"],
                "BeginCollectorMilliseconds": begin["TotalElapsedMilliseconds"],
                "EndCollectorMilliseconds": elapsed,
            })
    direct_totals = {}
    for child in intervals:
        parent = child["ParentBeginSequence"]
        direct_totals[parent] = direct_totals.get(parent, 0) + child["Milliseconds"]
    for interval in intervals:
        interval["MeasuredDirectChildMilliseconds"] = direct_totals.get(interval["BeginSequence"], 0)
        interval["UncoveredInclusiveRemainderMilliseconds"] = interval["Milliseconds"] - interval["MeasuredDirectChildMilliseconds"]
        assert interval["UncoveredInclusiveRemainderMilliseconds"] >= -0.001
    aggregated = []
    for operation in sorted({x["Operation"] for x in intervals}):
        durations = [x["Milliseconds"] for x in intervals if x["Operation"] == operation]
        aggregated.append({
            "Operation": operation, "CompletedCount": len(durations),
            "TotalMilliseconds": sum(durations), "MeanMilliseconds": statistics.mean(durations),
            "MinMilliseconds": min(durations), "MaxMilliseconds": max(durations),
            "SourceLocations": locations[operation],
        })
    aggregated.sort(key=lambda x: (-x["TotalMilliseconds"], x["Operation"]))
    last_normal = next(x["TotalElapsedMilliseconds"] for x in reversed(samples) if x["State"] != "limited")
    unfinished = [{
        "Operation": x["Operation"], "BeginSequence": x["Sequence"],
        "BeginCollectorMilliseconds": x["TotalElapsedMilliseconds"],
        "CompletedDurationMilliseconds": None,
        "ObservedCollectorWindowMilliseconds": last_normal - x["TotalElapsedMilliseconds"],
        "SourceLocations": locations[x["Operation"]],
    } for x in pending]
    assert not limited
    actual_stages = outcome["Children"][children.index(resumer)]["Stages"]
    assert not any(x.get("Transition") == "limit" or x.get("ResumeActivity", {}).get("Limited") for x in actual_stages)
    current_dlls = prep["Binding"] if report_prefix in ("T311", "T316", "T324") else prep["Binding"]["Binaries"]
    if verify_live:
        for binary in current_dlls:
            path = repo / binary["Path"]
            if report_prefix not in ("T316", "T324"):
                assert path.stat().st_size == binary["Bytes"]
            assert digest(path.read_bytes()) == binary["SHA256"]
    seed = outcome["SeedBinding"]
    generated_archive = evidence / (generation + "-generator.zip")
    assert digest(generated_archive.read_bytes()) == seed["GeneratorArchiveSHA256"]
    with zipfile.ZipFile(generated_archive) as source_zip:
        assert source_zip.testzip() is None
        assert digest(source_zip.read("generated/artifact/manifest.json")) == seed["ManifestSha256"]
        prefix = "generated/artifact/payload/"
        actual_files = sorted(x.filename[len(prefix):] for x in source_zip.infolist() if x.filename.startswith(prefix) and not x.is_dir())
        assert actual_files == sorted(x["path"] for x in seed["PayloadFiles"])
        for member in seed["PayloadFiles"]:
            raw = source_zip.read(prefix + member["path"])
            assert len(raw) == member["length"] and digest(raw) == member["sha256"]
    if verify_live:
        artifact = repo / seed["ArtifactRelativePath"]
        assert digest((artifact / "manifest.json").read_bytes()) == seed["ManifestSha256"]
        for member in seed["PayloadFiles"]:
            raw = (artifact / "payload" / member["path"]).read_bytes()
            assert len(raw) == member["length"] and digest(raw) == member["sha256"]
    assert outcome["BinariesUnchanged"]
    report = {
        "SchemaVersion": 1, "CompiledSource": outcome["CompiledSource"], "MetadataHead": outcome["MetadataHead"],
        "Run": outcome["Run"], "CaseOutcome": outcome["Outcome"], "CaseQualified": outcome["CaseQualified"],
        "TestDuration": outcome["TestDuration"], "RunnerWallTime": outcome["Summary"]["WallTime"],
        "ResumerPID": resumer["Id"], "ResumerExitCode": resumer["ExitCode"],
        "ResumerFailure": resumer.get("Failure"), "SharedBudgetChecks": outcome["SharedBudgetChecks"],
        "RecordCount": len(samples), "OutputLimited": limited, "SequenceContiguous": True,
        "AllRecordsBoundToActualResumer": True, "StackPairingVerified": True,
        "LastCollectorMilliseconds": last_elapsed, "LastNormalCollectorMilliseconds": last_normal,
        "CompletedIntervals": intervals,
        "Operations": aggregated, "Unfinished": unfinished,
        "AllIndependentStageRecords": actual_stages,
        "NoNamedStageOrActivityCutoff": True,
        "InnermostObservedUnfinished": unfinished[-1] if unfinished else None,
        "ObservedLiteralOperations": sorted({x["Operation"] for x in samples if x["State"] != "limited"}),
        "InstrumentedLiteralOperationCount": sum(map(len, locations.values())),
        "RawArchive": archive_path.name, "RawArchiveSHA256": outcome["ArchiveSHA256"],
        "RawArchiveCrcValid": True, "RawArchiveIndex": archive_index, "RawStreamBindings": stream_bindings,
        "SeedManifestSha256": seed["ManifestSha256"], "HistoricalGeneratedSeedPayloadVerified": True,
        "BinaryBindingVerifiedAtRun": current_dlls, "FixtureCleanup": outcome["FixtureCleanup"],
        "ChildrenEofAndJoined": all(x["Eof"] and x["Joined"] for x in children),
        "ChildrenCleanupFailures": [x.get("CleanupFailure") for x in children if x.get("CleanupFailure")],
        "ParentGameReportCount": len(outcome["ParentGameReports"]),
        "LastIndependentStageRecord": outcome["Children"][children.index(resumer)]["Stages"][-1] if outcome["Children"][children.index(resumer)]["Stages"] else None,
        "OriginalWindowsFive": "0/5 PASS; not rerun by this investigation", "Linux": "UNRUN locally",
        "Limits": [
            "One instrumented run. Scope times are inclusive; nested rows must not be added as independent wall time.",
            "An end boundary means the scope ended, including exception unwinding; it does not certify operation success.",
            "Begin-only spans have no measured completed duration. The 600-second parent wait is a different clock and is not assigned to an unfinished method.",
            "An observed collector window is the difference between two output timestamps, including observer overhead; it is not the measured duration of a completed scope.",
            "Named, stage and activity records have no diagnostic quota. A terminated process cannot emit later end boundaries.",
            ("One redundant outer baseline was removed; retained operations still include observer/serialization overhead. No uninstrumented speedup is inferred."
             if report_prefix == "T311" else
             "One redundant story-source reread was replaced by exact authenticated readback comparison before initial proof issuance; later fresh validators remain. No controlled wall-time speedup is inferred."
             if report_prefix == "T324" else
             "Instrumentation preserves calls and assertions but adds observer/serialization overhead. No uninstrumented speedup or fix is claimed."),
            "Named operation durations localize code cost; they do not separate CPU, filesystem latency, scanning or environment effects.",
        ],
    }
    if "LiveCpuSamples" in outcome:
        cpu_member = stem + "-live-cpu.jsonl"
        observer_member = stem + "-cpu-observer.json"
        with zipfile.ZipFile(archive_path) as archive:
            cpu_samples = [json.loads(line) for line in archive.read(cpu_member).decode("utf-8-sig").splitlines() if line]
            cpu_observer = json.loads(archive.read(observer_member))
        assert cpu_samples == outcome["LiveCpuSamples"] and cpu_observer == outcome["LiveCpuObserver"]
        assert outcome["LiveCpuPIDAndBirthBoundToActualResumer"] and outcome["LiveCpuSamplesMonotonic"]
        assert all(x["PID"] == resumer["Id"] and x["BirthUtcTicks"] == resumer["WindowsStartTimeUtcTicks"] and not x["RuntimeModified"] for x in cpu_samples)
        assert cpu_observer["RecordQuota"] is None and cpu_observer["LifetimeQuota"] is None
        assert cpu_observer["State"] == "Completed" and outcome["LiveCpuObserverNaturalEnd"]
        for left, right in zip(cpu_samples, cpu_samples[1:]):
            assert right["ObservedUtc"] > left["ObservedUtc"]
            assert all(right[key] >= left[key] >= 0 for key in ("TotalProcessorSeconds", "UserProcessorSeconds", "PrivilegedProcessorSeconds"))
        first, last = cpu_samples[0], cpu_samples[-1]
        nominal_wall = (datetime.fromisoformat(last["ObservedUtc"]) - datetime.fromisoformat(first["ObservedUtc"])).total_seconds()
        assert nominal_wall > 0
        report["ProcessCpuObservation"] = {
            "Samples": cpu_samples, "SampleCount": len(cpu_samples), "ActualPIDAndBirthVerified": True,
            "ArchiveMember": cpu_member, "Observer": cpu_observer,
            "FirstObservedUtc": first["ObservedUtc"], "LastObservedUtc": last["ObservedUtc"],
            "NominalUtcWindowSeconds": nominal_wall,
            "WindowTotalCpuSeconds": last["TotalProcessorSeconds"] - first["TotalProcessorSeconds"],
            "WindowUserCpuSeconds": last["UserProcessorSeconds"] - first["UserProcessorSeconds"],
            "WindowPrivilegedCpuSeconds": last["PrivilegedProcessorSeconds"] - first["PrivilegedProcessorSeconds"],
            "LastAccumulatedTotalCpuSeconds": last["TotalProcessorSeconds"],
            "LastAccumulatedUserCpuSeconds": last["UserProcessorSeconds"],
            "LastAccumulatedPrivilegedCpuSeconds": last["PrivilegedProcessorSeconds"],
            "Limitations": outcome["LiveCpuLimitations"],
        }
    return report


def markdown(report, report_prefix="T302"):
    lines = [
        "# Именованные замеры восстановления — " + report_prefix, "",
        f"Один фактический запуск: `{report['Run']}`. Результат случая: **{report['CaseOutcome']}**; "
        f"тест {report['TestDuration']}, весь запуск {report['RunnerWallTime']}.", "",
        f"Исходники: `{report['CompiledSource']}`; сохранённое состояние запуска: `{report['MetadataHead']}`. "
        f"PID восстановления: {report['ResumerPID']}. "
        f"Получено {report['RecordCount']} записей без квоты диагностического вывода. "
        f"Последняя запись по часам коллектора: {report['LastCollectorMilliseconds']/1000:.6f} с.", "",
        "## Завершённые интервалы", "",
        "Значения включают вложенные вызовы. Строки нельзя складывать как независимые части общего времени. "
        "Конец интервала также записывается при выходе через исключение и сам по себе не означает успешную операцию.", "",
        "| Участок кода | Завершено вызовов | Сумма, с | Среднее, с | Максимум, с |",
        "| --- | ---: | ---: | ---: | ---: |",
    ]
    for value in report["Operations"]:
        lines.append(f"| `{value['Operation']}` | {value['CompletedCount']} | {value['TotalMilliseconds']/1000:.6f} | {value['MeanMilliseconds']/1000:.6f} | {value['MaxMilliseconds']/1000:.6f} |")
    lines += ["", "## Начатые интервалы без записи конца", ""]
    if report["Unfinished"]:
        lines += ["Внешний → внутренний. Длительность завершения не измерена; родительский лимит 600 с не приписан отдельному методу.", "",
                  "| Участок кода | Номер начала | Начало по часам коллектора, с | Наблюдаемое окно до последней обычной записи, с |",
                  "| --- | ---: | ---: | ---: |"]
        for value in report["Unfinished"]:
            lines.append(f"| `{value['Operation']}` | {value['BeginSequence']} | {value['BeginCollectorMilliseconds']/1000:.6f} | {value['ObservedCollectorWindowMilliseconds']/1000:.6f} |")
        lines += ["", "Наблюдаемое окно — разность двух отметок вывода одних часов, с накладными расходами наблюдателя; это не завершённая длительность метода."]
        lines += ["", f"Самый внутренний наблюдавшийся незавершённый участок: **`{report['Unfinished'][-1]['Operation']}`**."]
        if report["OutputLimited"]:
            lines += ["Лимит вывода достигнут: это последнее наблюдение, а не доказательство участка, активного при тайм-ауте."]
    else:
        lines += ["Все начатые именованные интервалы имеют запись конца."]
    lines += ["", "## Проверка и границы вывода", "",
              "Идентификатор процесса, последовательность записей и вложенность проверены по сырому stdout фактического дочернего восстановления. "
              "Извлечённые потоки побайтно сверены с TRX внутри архива; CRC, уникальность имён и SHA-256 каждого файла архива проверены. " +
              (f"{len(report['BinaryBindingVerifiedAtRun'])} DLL" if report_prefix in ("T316", "T324") else "Три DLL") +
              " сверены родительским отчётом до и после запуска; файлы исходного снимка повторно сверены в сохранённом генераторском архиве.", "",
              f"Сырой архив: [{report['RawArchive']}]({report['RawArchive']}); SHA-256 `{report['RawArchiveSHA256']}`. "
              f"Полные интервалы, ссылки на строки исходников и индекс архива: [{report_prefix}-timings.json]({report_prefix}-timings.json).", "",
              f"EOF/join обоих процессов: {report['ChildrenEofAndJoined']}; ошибок завершения контейнеров: {len(report['ChildrenCleanupFailures'])}; "
              f"отчётов с игровым результатом: {report['ParentGameReportCount']}.", ""]
    last_stage = report["LastIndependentStageRecord"]
    if last_stage:
        lines += [f"Отдельный существующий канал стадий зафиксировал "
                  f"`{last_stage['Stage']}/{last_stage['Transition']}` "
                  f"(`{last_stage.get('Checkpoint')}`) на {last_stage['ElapsedMilliseconds']/1000:.6f} с своих часов. "
                  "Это отдельное наблюдение контрольной точки; его часы имеют другое начало отсчёта. Оно не является игровым PASS.", ""]
    for value in report["FixtureCleanup"]:
        lines.append(f"- Удаление собственного тестового корня: {value['OwnedFixtureRemoved']}; отсутствие подтверждено: {value['PathAbsentOnParentCheck']}.")
    if "ProcessCpuObservation" in report:
        cpu = report["ProcessCpuObservation"]
        lines += ["", "## Счётчики CPU того же дочернего процесса", "",
                  f"Все {cpu['SampleCount']} снимка связаны с фактическим PID и точным временем рождения из native-отчёта. "
                  "Наблюдатель читал стандартные счётчики процесса каждые 15 с до его выхода; квоты количества или длительности не было. "
                  "Первый отдельный снимок сделан до запуска периодического наблюдателя.", "",
                  f"Последний снимок `{cpu['LastObservedUtc']}`: накоплено CPU {cpu['LastAccumulatedTotalCpuSeconds']:.6f} с, "
                  f"пользовательское время {cpu['LastAccumulatedUserCpuSeconds']:.6f} с, время ядра {cpu['LastAccumulatedPrivilegedCpuSeconds']:.6f} с. "
                  f"Между первым и последним снимками: CPU {cpu['WindowTotalCpuSeconds']:.6f} с за {cpu['NominalUtcWindowSeconds']:.6f} с по UTC.", "",
                  "Это процессные суммы всех потоков, не CPU отдельного именованного интервала. Значения считываются последовательно; "
                  "небольшая разница total и user+kernel возможна из-за продвижения счётчиков. UTC, часы коллектора и родительского ожидания различаются. "
                  "Время ядра не устанавливает конкретный драйвер, системный вызов или влияние антивируса. "
                  "Снимки, состояние и исходник наблюдателя сохранены в сыром архиве."]
    change_scope = ("Удалена одна внешняя baseline-проверка; лимиты не увеличивались. "
                    if report_prefix == "T311" else
                    "Повторное чтение при первоначальной выдаче story-proof заменено полным сравнением с проверенным readback; последующие проверки остаются свежими. Лимиты не увеличивались. "
                    if report_prefix == "T324" else "Оптимизаций и увеличения лимитов в этом блоке нет. ")
    lines += ["", "Это замер одного запуска с наблюдателями. Он показывает стоимость участков, но не разделяет работу CPU, файловой системы и внешних проверок. "
              + change_scope + "Исходные пять Windows-случаев: 0/5 PASS; Linux локально не запускался.", ""]
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[5])
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--verify-live", action="store_true", help="Additionally verify currently installed DLLs and fixture bytes; omit for historical offline reconstruction")
    parser.add_argument("--report", choices=("T302", "T306", "T311", "T316", "T324"), default="T302", help="Select the saved measurement; never executes or retries it")
    parser.add_argument("--refresh-derived", action="store_true", help="Refresh only derived reports after an offline report clarification; never changes raw evidence")
    args = parser.parse_args()
    assert not (args.check and args.refresh_derived)
    report = build_report(args.repo, args.verify_live, args.report)
    output = args.repo / "specs/1536-complete-wound-materialization/recovery/crash5"
    json_path = output / (args.report + "-timings.json")
    md_path = output / (args.report + "-timings.md")
    content = markdown(report, args.report)
    if args.check:
        assert load(json_path) == report
        assert md_path.read_text(encoding="utf-8-sig") == content
    else:
        if args.refresh_derived:
            previous = load(json_path)
            assert previous["Run"] == report["Run"] and previous["RawArchiveSHA256"] == report["RawArchiveSHA256"]
        else:
            assert not json_path.exists() and not md_path.exists(), "Use explicit --refresh-derived for a derived-report clarification"
        json_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        md_path.write_text(content, encoding="utf-8")
    print(json.dumps({key: report[key] for key in ("Run", "CaseOutcome", "RecordCount", "OutputLimited", "InnermostObservedUnfinished")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
