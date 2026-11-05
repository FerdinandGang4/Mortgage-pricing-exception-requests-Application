FROM node:24-alpine AS build
WORKDIR /app
COPY FrontEnd/package*.json ./
RUN npm ci
COPY FrontEnd/ ./
ARG VITE_ENABLE_DEMO_USERS=false
ENV VITE_ENABLE_DEMO_USERS=$VITE_ENABLE_DEMO_USERS
RUN npm run build

FROM nginx:stable-alpine
COPY FrontEnd/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
