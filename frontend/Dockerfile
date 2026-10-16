# pitchside-web: the Angular production build served by nginx, which also proxies /api, /hubs and /mcp to the API
# named by API_HOST. Build context is this folder.
#   docker build -t pitchside/web frontend

FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

# The unprivileged image runs as a non-root user and listens on 8080. Its entrypoint renders
# /etc/nginx/templates/*.template into /etc/nginx/conf.d/ with the defined environment variables, here API_HOST.
FROM nginxinc/nginx-unprivileged:1.29-alpine AS final
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
EXPOSE 8080
