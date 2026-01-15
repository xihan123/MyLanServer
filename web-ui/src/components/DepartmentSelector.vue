<template>
  <div class="department-selector">
    <input
        :value="modelValue"
        placeholder="请选择您的所属单位/部门"
        readonly
        required
        type="text"
        @click="showDropdown = true"
        :aria-activedescendant="focusedIndex >= 0 ? `dept-${focusedIndex}` : undefined"
        :aria-expanded="showDropdown ? 'true' : 'false'"
        aria-autocomplete="list"
        aria-haspopup="listbox"
        role="combobox"
        @keydown="handleKeyDown"
    />
    <input
        v-if="showDropdown"
        v-model="searchTerm"
        class="department-search"
        placeholder="搜索部门..."
        type="text"
        @blur="handleBlur"
        aria-controls="department-list"
        role="searchbox"
        @keydown="handleKeyDown"
    />
    <div v-if="showDropdown" class="department-dropdown" role="listbox">
      <ul id="department-list" class="department-list">
        <li
            v-for="(dept, index) in filteredDepartments"
            :key="dept.id"
            :class="{ selected: modelValue === dept.name }"
            :id="`dept-${index}`"
            :aria-selected="modelValue === dept.name"
            @mousedown.prevent="selectDepartment(dept)"
            role="option"
            tabindex="-1"
        >
          {{ dept.name }}
        </li>
        <li v-if="filteredDepartments.length === 0" class="no-results" role="status">
          没有找到匹配的部门
        </li>
      </ul>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {computed, onMounted, onUnmounted, ref} from 'vue';
import {useDebounceFn} from '@vueuse/core';
import {type Department, useDepartments} from '../composables/useDepartments';

const props = defineProps<{ modelValue: string }>();
const emit = defineEmits<{ 'update:modelValue': [value: string] }>();

const {departments, loadDepartments} = useDepartments();
const showDropdown = ref(false);
const searchTerm = ref('');
const focusedIndex = ref(-1);

const filteredDepartments = computed(() => {
  if (!searchTerm.value) return departments.value;
  const term = searchTerm.value.toLowerCase();
  return departments.value.filter((d: Department) => d.name.toLowerCase().includes(term));
});

const selectDepartment = (dept: Department) => {
  emit('update:modelValue', dept.name);
  showDropdown.value = false;
  searchTerm.value = '';
  focusedIndex.value = -1;
};

const handleBlur = useDebounceFn(() => {
  showDropdown.value = false;
  focusedIndex.value = -1;
}, 200) as unknown as (() => void) & { cancel: () => void };

// 键盘导航处理
const handleKeyDown = (e: KeyboardEvent) => {
  if (!showDropdown.value) return;

  const items = filteredDepartments.value;
  if (items.length === 0) return;

  switch (e.key) {
    case 'ArrowDown':
      e.preventDefault();
      focusedIndex.value = focusedIndex.value < items.length - 1 ? focusedIndex.value + 1 : 0;
      break;
    case 'ArrowUp':
      e.preventDefault();
      focusedIndex.value = focusedIndex.value > 0 ? focusedIndex.value - 1 : items.length - 1;
      break;
    case 'Enter':
    case ' ':
      e.preventDefault();
      if (focusedIndex.value >= 0 && focusedIndex.value < items.length) {
        selectDepartment(items[focusedIndex.value]);
      }
      break;
    case 'Escape':
      e.preventDefault();
      showDropdown.value = false;
      focusedIndex.value = -1;
      break;
  }
};

onMounted(() => loadDepartments());

// 组件卸载时清理 debounce
onUnmounted(() => {
  handleBlur.cancel();
});
</script>
