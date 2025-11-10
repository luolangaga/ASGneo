<script setup>
</script>

<template>
  <v-app>
    <v-app-bar app color="primary" dark>
      <v-toolbar-title>管理后台</v-toolbar-title>
      <v-spacer></v-spacer>
      <v-btn to="/" variant="text">首页</v-btn>
      <v-btn to="/about" variant="text">关于</v-btn>
    </v-app-bar>
    <v-main>
      <router-view />
    </v-main>
  </v-app>
  
</template>

<style scoped>
</style>
