<script setup>
import { ref, watch } from 'vue';

const props = defineProps({
  modelValue: {
    type: Object,
    required: true,
    default: () => ({ name: '', gameId: '', gameRank: '', description: '', playerType: 2 }),
  },
  removable: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['update:modelValue', 'remove']);

const player = ref(props.modelValue);

watch(() => props.modelValue, (newValue) => {
  player.value = newValue;
}, { deep: true });

watch(player, (newValue) => {
  emit('update:modelValue', newValue);
}, { deep: true });

function onRemove() {
  emit('remove');
}
</script>

<template>
  <v-card variant="tonal" class="mb-3">
    <v-card-text>
      <v-row>
        <v-col cols="12" md="4">
          <v-text-field v-model="player.name" label="队员昵称" prepend-inner-icon="person" required />
        </v-col>
        <v-col cols="12" md="4">
          <v-text-field v-model="player.gameId" label="游戏ID" prepend-inner-icon="sports_esports" />
        </v-col>
        <v-col cols="12" md="4">
          <v-text-field v-model="player.gameRank" label="段位/等级" prepend-inner-icon="star" />
        </v-col>
        <v-col cols="12" md="4">
          <v-select
            v-model="player.playerType"
            label="角色类型"
            :items="[{ value: 2, title: '求生者' }, { value: 1, title: '监管者' }]"
            prepend-inner-icon="sports"
          />
        </v-col>
        <v-col cols="12">
          <v-textarea v-model="player.description" label="简介" prepend-inner-icon="text_fields" rows="2" />
        </v-col>
      </v-row>
      <div class="d-flex justify-end" v-if="removable">
        <v-btn color="error" variant="text" @click="onRemove" prepend-icon="delete">移除</v-btn>
      </div>
    </v-card-text>
  </v-card>
</template>
