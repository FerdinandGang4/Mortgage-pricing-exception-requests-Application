FROM node:24-alpine AS build
WORKDIR /app
COPY FrontEnd/package*.json ./
RUN npm ci
COPY FrontEnd/ ./
RUN npm run build

FROM nginx:stable-alpine
COPY FrontEnd/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
