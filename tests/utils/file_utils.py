#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
文件工具类
提供文件操作相关的工具方法
"""

import os
from typing import Tuple


class FileUtils:
    """文件工具类"""

    @staticmethod
    def save_file(filepath: str, content: bytes) -> Tuple[bool, str]:
        """
        保存文件

        Args:
            filepath: 文件路径
            content: 文件内容

        Returns:
            (成功标志, 消息)
        """
        try:
            # 确保目录存在
            os.makedirs(os.path.dirname(filepath), exist_ok=True)

            # 写入文件
            with open(filepath, 'wb') as f:
                f.write(content)

            return True, f'文件保存成功: {filepath}'
        except Exception as e:
            return False, f'文件保存失败: {str(e)}'

    @staticmethod
    def format_file_size(size_bytes: int) -> str:
        """
        格式化文件大小

        Args:
            size_bytes: 文件大小（字节）

        Returns:
            格式化后的文件大小字符串
        """
        if size_bytes == 0:
            return '0 B'

        units = ['B', 'KB', 'MB', 'GB', 'TB']
        unit_index = 0
        size = float(size_bytes)

        while size >= 1024 and unit_index < len(units) - 1:
            size /= 1024
            unit_index += 1

        return f'{size:.2f} {units[unit_index]}'

    @staticmethod
    def get_file_extension(filename: str) -> str:
        """
        获取文件扩展名

        Args:
            filename: 文件名

        Returns:
            文件扩展名（包含点号，如 .xlsx）
        """
        _, ext = os.path.splitext(filename)
        return ext.lower()

    @staticmethod
    def validate_file_size(file_size: int, max_size: int) -> Tuple[bool, str]:
        """
        验证文件大小

        Args:
            file_size: 文件大小（字节）
            max_size: 最大允许大小（字节）

        Returns:
            (验证通过, 消息)
        """
        if file_size <= 0:
            return False, f'文件大小无效: {FileUtils.format_file_size(file_size)}'

        if file_size > max_size:
            return False, f'文件大小超过限制: {FileUtils.format_file_size(file_size)} > {FileUtils.format_file_size(max_size)}'

        return True, f'文件大小验证通过: {FileUtils.format_file_size(file_size)}'

    @staticmethod
    def validate_file_extension(filename: str, allowed_extensions: list) -> Tuple[bool, str]:
        """
        验证文件扩展名

        Args:
            filename: 文件名
            allowed_extensions: 允许的扩展名列表（如 ['.xlsx', '.xls']）

        Returns:
            (验证通过, 消息)
        """
        ext = FileUtils.get_file_extension(filename)

        if not ext:
            return False, f'文件名无效: {filename}'

        if ext not in allowed_extensions:
            return False, f'文件扩展名不支持: {ext}（允许的扩展名: {", ".join(allowed_extensions)}）'

        return True, f'文件扩展名验证通过: {ext}'

    @staticmethod
    def get_filename_without_extension(filename: str) -> str:
        """
        获取不带扩展名的文件名

        Args:
            filename: 文件名

        Returns:
            不带扩展名的文件名
        """
        name, _ = os.path.splitext(filename)
        return name

    @staticmethod
    def join_path(*paths: str) -> str:
        """
        拼接路径

        Args:
            *paths: 路径片段

        Returns:
            拼接后的路径
        """
        return os.path.join(*paths)

    @staticmethod
    def ensure_dir_exists(dirpath: str) -> bool:
        """
        确保目录存在

        Args:
            dirpath: 目录路径

        Returns:
            是否成功创建或已存在
        """
        try:
            os.makedirs(dirpath, exist_ok=True)
            return True
        except Exception:
            return False

    @staticmethod
    def file_exists(filepath: str) -> bool:
        """
        检查文件是否存在

        Args:
            filepath: 文件路径

        Returns:
            文件是否存在
        """
        return os.path.isfile(filepath)

    @staticmethod
    def dir_exists(dirpath: str) -> bool:
        """
        检查目录是否存在

        Args:
            dirpath: 目录路径

        Returns:
            目录是否存在
        """
        return os.path.isdir(dirpath)