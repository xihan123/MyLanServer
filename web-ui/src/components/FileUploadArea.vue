<template>
  <div
      :aria-label="ariaLabel"
      :class="{ 'dragover': isDragging }"
      class="upload-area"
      role="button"
      tabindex="0"
      @click="handleClick"
      @dragover.prevent="handleDragOver"
      @dragleave.prevent="handleDragLeave"
      @drop.prevent="handleDrop"
      @keypress.enter="handleClick"
      @keypress.space.prevent="handleClick"
  >
    <div class="upload-icon">{{ icon }}</div>
    <div class="upload-hint">{{ hint }}</div>
    <input
        ref="fileInputRef"
        :accept="acceptAttribute"
        :multiple="multiple"
        class="hidden"
        type="file"
        @change="handleFileSelect"
    />
    <div v-if="selectedFiles.length > 0" class="file-list">
      <div v-for="file in selectedFiles" :key="file.id" class="file-item">
        <div :title="file.file.name" class="file-item-name">{{ file.file.name }}</div>
        <div class="file-item-size">{{ formatFileSize(file.file.size) }}</div>
        <div aria-label="移除文件" class="file-item-remove" title="移除文件" @click="removeFile(file.id)">×</div>
      </div>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {ref} from 'vue';
import {formatFileSize} from '../utils/api';

export interface FileUploadAreaProps {
  acceptAttribute?: string;
  multiple?: boolean;
  icon?: string;
  hint?: string;
  ariaLabel?: string;
}

export interface FileItem {
  file: File;
  id: string;
}

const props = withDefaults(defineProps<FileUploadAreaProps>(), {
  acceptAttribute: '',
  multiple: false,
  icon: '📎',
  hint: '点击选择文件或拖拽文件到此处',
  ariaLabel: '文件上传区域'
});

const emit = defineEmits<{
  'update:selectedFiles': [files: File[]];
}>();

const fileInputRef = ref<HTMLInputElement>();
const selectedFiles = ref<FileItem[]>([]);
const isDragging = ref(false);

const handleClick = () => {
  fileInputRef.value?.click();
};

const handleDragOver = (e: DragEvent) => {
  e.preventDefault();
  isDragging.value = true;
};

const handleDragLeave = (e: DragEvent) => {
  e.preventDefault();
  isDragging.value = false;
};

const handleDrop = (e: DragEvent) => {
  e.preventDefault();
  isDragging.value = false;
  const files = e.dataTransfer?.files;
  if (files) {
    addFiles(Array.from(files));
  }
};

const handleFileSelect = (e: Event) => {
  const files = (e.target as HTMLInputElement).files;
  if (files) {
    addFiles(Array.from(files));
  }
};

const addFiles = (files: File[]) => {
  files.forEach(file => {
    const id = `${Date.now()}-${Math.random().toString(36).substring(2)}`;
    selectedFiles.value.push({file, id});
  });
  emit('update:selectedFiles', selectedFiles.value.map(f => f.file));
};

const removeFile = (id: string) => {
  const index = selectedFiles.value.findIndex(f => f.id === id);
  if (index > -1) {
    selectedFiles.value.splice(index, 1);
    const remainingFiles = selectedFiles.value.map(f => f.file);
    emit('update:selectedFiles', remainingFiles);
  }
};

const reset = () => {
  selectedFiles.value = [];
  if (fileInputRef.value) {
    fileInputRef.value.value = '';
  }
};

defineExpose({
  reset
});
</script>
