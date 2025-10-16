export const environment = {
  production: false,
  // apiUrl: 'https://localhost:44325/api', // backend API base
  // hubUrl: 'https://localhost:44325', // IMPORTANT: HTTPS to match server
  // go through the dev proxy
  apiUrl: '/api',

  // important: this is ONLY the /hubs base.
  // ChatService will append "/chat" → "/hubs/chat"
  hubUrl: '/hubs',
  useMocks: false,
};
