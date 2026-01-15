#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
测试报告生成器
支持生成 Markdown 格式的测试报告
"""

import os
import json
from datetime import datetime
from typing import List, Dict, Any
from .test_base import TestResult, TestConfig


class ReportGenerator:
    """测试报告生成器"""

    def __init__(self, output_dir: str):
        self.output_dir = output_dir

    def generate_markdown_report(
        self,
        results: List[TestResult],
        config: TestConfig,
        additional_info: Dict[str, Any] = None
    ) -> str:
        """
        生成 Markdown 格式的测试报告

        Args:
            results: 测试结果列表
            config: 测试配置
            additional_info: 额外信息（如 Schema 信息、任务信息等）

        Returns:
            Markdown 格式的报告内容
        """
        report_lines = []

        # 标题
        report_lines.append('# 功能测试报告\n')

        # 测试概要
        report_lines.append('## 测试概要\n')
        report_lines.append(f'- **测试时间**: {datetime.now().strftime("%Y-%m-%d %H:%M:%S")}')
        report_lines.append(f'- **测试环境**: {config.base_api}')
        report_lines.append(f'- **任务 Slug**: {config.slug}')
        report_lines.append(f'- **批量提交**: {config.batch_count} 次, {config.concurrent} 并发')
        report_lines.append(f'- **测试用例总数**: {len(results)}')
        report_lines.append(f'- **通过用例数**: {sum(1 for r in results if r.passed)}')
        report_lines.append(f'- **失败用例数**: {sum(1 for r in results if not r.passed)}')

        if len(results) > 0:
            report_lines.append(f'- **通过率**: {sum(1 for r in results if r.passed) / len(results) * 100:.1f}%\n')
        else:
            report_lines.append(f'- **通过率**: 0%\n')

        # 测试配置
        report_lines.append('## 测试配置\n')
        report_lines.append('| 配置项 | 值 |')
        report_lines.append('|-------|-----|')
        report_lines.append(f'| Base API | {config.base_api} |')
        report_lines.append(f'| Slug | {config.slug} |')
        report_lines.append(f'| 密码 | *** |')
        report_lines.append(f'| 测试用户 | {config.test_user["name"]}, {config.test_user["contact"]}, {config.test_user["department"]} |')
        report_lines.append(f'| 批量提交次数 | {config.batch_count} |')
        report_lines.append(f'| 并发数 | {config.concurrent} |\n')

        # 额外信息（如 Schema 信息、任务信息等）
        if additional_info:
            self._append_additional_info(report_lines, additional_info)

        # 测试用例详情
        report_lines.append('## 测试用例详情\n')

        for i, result in enumerate(results, 1):
            status_icon = '✅' if result.passed else '❌'
            report_lines.append(f'### {i}. {result.test_name} {status_icon}\n')
            report_lines.append(f'**测试时间**: {datetime.now().strftime("%Y-%m-%d %H:%M:%S")}')
            report_lines.append(f'**响应时间**: {result.duration:.2f}s')
            report_lines.append(f'**状态**: {"通过" if result.passed else "失败"}\n')
            report_lines.append(f'**测试结果**: {result.message}\n')

            if result.error:
                report_lines.append(f'**错误信息**: {result.error}\n')

            if result.details:
                report_lines.append('**详细信息**:\n')
                self._format_details(report_lines, result.details)

            if result.response_data:
                report_lines.append('**响应数据**:\n')
                report_lines.append('```json')
                report_lines.append(json.dumps(result.response_data, ensure_ascii=False, indent=2))
                report_lines.append('```\n')

            report_lines.append('---\n')

        # 性能统计
        if len(results) > 0:
            report_lines.append('## 性能统计\n')
            report_lines.append('| 指标 | 值 |')
            report_lines.append('|-----|-----|')
            report_lines.append(f'| 平均响应时间 | {sum(r.duration for r in results) / len(results):.2f}s |')
            report_lines.append(f'| 最大响应时间 | {max(r.duration for r in results):.2f}s |')
            report_lines.append(f'| 最小响应时间 | {min(r.duration for r in results):.2f}s |')
            report_lines.append(f'| 总测试时间 | {sum(r.duration for r in results):.2f}s |\n')

        # 问题汇总
        failed_results = [r for r in results if not r.passed]
        if failed_results:
            report_lines.append('## 问题汇总\n')
            report_lines.append('| 测试用例 | 错误信息 |')
            report_lines.append('|---------|---------|')
            for result in failed_results:
                error = result.error or '未知错误'
                report_lines.append(f'| {result.test_name} | {error} |\n')
        else:
            report_lines.append('## 问题汇总\n')
            report_lines.append('✅ 所有的测试用例都通过了！\n')

        # 测试结论
        report_lines.append('## 测试结论\n')
        if all(r.passed for r in results):
            report_lines.append('✅ **测试通过**: 所有功能正常工作\n')
        else:
            passed_count = sum(1 for r in results if r.passed)
            report_lines.append(f'⚠️ **部分通过**: {passed_count}/{len(results)} 个测试用例通过\n')

        return '\n'.join(report_lines)

    def _append_additional_info(self, report_lines: List[str], additional_info: Dict[str, Any]):
        """
        添加额外信息到报告

        Args:
            report_lines: 报告行列表
            additional_info: 额外信息字典
        """
        # Schema 信息（在线填表模式）
        if 'schema' in additional_info:
            schema_data = additional_info['schema']
            if schema_data:
                report_lines.append('## Schema 信息\n')
                report_lines.append(f'- **标题**: {schema_data.get("title", "N/A")}')
                report_lines.append(f'- **字段数量**: {len(schema_data.get("columns", []))}')
                report_lines.append(f'- **允许附件上传**: {"是" if schema_data.get("allowAttachmentUpload", False) else "否"}\n')

                if schema_data.get('columns'):
                    report_lines.append('| 字段名称 | 字段类型 | 必填 |')
                    report_lines.append('|---------|---------|------|')
                    for column in schema_data['columns']:
                        report_lines.append(f'| {column.get("name", "N/A")} | {column.get("type", "N/A")} | {"是" if column.get("required", False) else "否"} |')
                    report_lines.append('')

        # 任务信息（文件收集模式）
        if 'task_info' in additional_info:
            task_info = additional_info['task_info']
            if task_info:
                report_lines.append('## 任务信息\n')
                report_lines.append(f'- **任务标题**: {task_info.get("title", "N/A")}')
                report_lines.append(f'- **任务类型**: {"文件收集" if task_info.get("taskType") == 0 else "在线填表"}')
                report_lines.append(f'- **允许扩展名**: {", ".join(task_info.get("allowedExtensions", []))}')
                report_lines.append(f'- **允许附件上传**: {"是" if task_info.get("allowAttachmentUpload", False) else "否"}')
                report_lines.append(f'- **版本控制模式**: {task_info.get("versioningMode", "N/A")}\n')

        # 模板信息（文件收集模式）
        if 'template_info' in additional_info:
            template_info = additional_info['template_info']
            if template_info:
                report_lines.append('## 模板信息\n')
                report_lines.append(f'- **模板文件名**: {template_info.get("filename", "N/A")}')
                report_lines.append(f'- **表头数量**: {template_info.get("header_count", 0)}')
                report_lines.append(f'- **表头列表**: {", ".join(template_info.get("headers", []))}\n')

    def _format_details(self, report_lines: List[str], details: Dict, indent: int = 0):
        """
        格式化详细信息

        Args:
            report_lines: 报告行列表
            details: 详细信息字典
            indent: 缩进级别
        """
        prefix = '  ' * indent

        for key, value in details.items():
            if isinstance(value, dict):
                report_lines.append(f'{prefix}- **{key}**:')
                self._format_details(report_lines, value, indent + 1)
            elif isinstance(value, list):
                report_lines.append(f'{prefix}- **{key}**:')
                for item in value:
                    if isinstance(item, dict):
                        self._format_details(report_lines, item, indent + 1)
                    else:
                        report_lines.append(f'{prefix}  - {item}')
            else:
                report_lines.append(f'{prefix}- **{key}**: {value}')

    def save_report(self, content: str, filename: str):
        """
        保存报告到文件

        Args:
            content: 报告内容
            filename: 文件名

        Returns:
            保存的文件路径
        """
        filepath = os.path.join(self.output_dir, filename)
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        return filepath